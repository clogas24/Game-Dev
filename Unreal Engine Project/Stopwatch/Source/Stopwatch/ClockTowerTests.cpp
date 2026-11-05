// Copyright Epic Games, Inc. All Rights Reserved.

#include "BrokenBridge.h"
#include "Camera/PlayerCameraManager.h"
#include "Components/MeshComponent.h"
#include "Components/StaticMeshComponent.h"
#include "DropChute.h"
#include "Engine/TargetPoint.h"
#include "FallingPlatform.h"
#include "FreezePendulum.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "HourglassPickup.h"
#include "LevelTriggers.h"
#include "Misc/AutomationTest.h"
#include "PhysicsProp.h"
#include "Resettable.h"
#include "RollTrigger.h"
#include "StopwatchCharacter.h"
#include "StopwatchGameMode.h"
#include "StopwatchPlayerController.h"
#include "StopwatchTests.h"
#include "TimeAbilityComponent.h"
#include "TimeableComponent.h"
#include "TimeSandComponent.h"
#include "Tests/AutomationCommon.h"

#if WITH_DEV_AUTOMATION_TESTS

using namespace StopwatchTests;

namespace ClockTowerTests
{
	UWorld* FindClockTowerWorld()
	{
		return FindGameWorld(TEXT("Lvl_ClockTower"));
	}

	constexpr int32 SectionCount = 5;

	/** Must match build_level.py: how far above the walkway the chutes and the block start */
	constexpr double ChuteHeight = 650.0;
	constexpr double BlockHeight = 510.0;

	/** The block works as a stair while its top is in this band above its floor, see the jumps marked around it */
	constexpr double BlockBandLow = 140.0;
	constexpr double BlockBandHigh = 200.0;

	/** A crate is usable while its top is this close to the walkway: the player can step down onto it and jump up from it */
	constexpr double CrateBand = 100.0;

	constexpr double MinWindowSeconds = 0.6;

	/** True if a trace from Eye reaches any of Target's meshes */
	bool IsInView(UWorld* World, const FVector& Eye, const AActor* Target, const AActor* Ignore)
	{
		TInlineComponentArray<UMeshComponent*> Meshes(const_cast<AActor*>(Target));

		for (const UMeshComponent* Mesh : Meshes)
		{
			FHitResult Hit;
			FCollisionQueryParams Params(SCENE_QUERY_STAT(TutorialView), false, Ignore);

			if (!World->LineTraceSingleByChannel(Hit, Eye, Mesh->Bounds.Origin, ECC_Visibility, Params) || Hit.GetActor() == Target)
			{
				return true;
			}
		}

		return false;
	}

	UPrimitiveComponent* FindPart(const AActor* Actor, FName Name)
	{
		TInlineComponentArray<UPrimitiveComponent*> Parts(const_cast<AActor*>(Actor));
		UPrimitiveComponent* const* Part = Parts.FindByPredicate([Name](const UPrimitiveComponent* Candidate) { return Candidate->GetFName() == Name; });
		return Part ? *Part : nullptr;
	}

	/** Points the view at Point. The aim trace uses the view, which follows a frame later */
	void AimAt(APlayerController* Controller, const FVector& Point)
	{
		Controller->SetControlRotation((Point - Controller->PlayerCameraManager->GetCameraLocation()).Rotation());
	}
}

using namespace ClockTowerTests;

/**
 *  What the level must hold, checked on the saved level. Needs the game running it:
 *  UnrealEditor-Cmd.exe Stopwatch.uproject /Game/Stopwatch/Maps/Lvl_ClockTower -game -nullrhi -unattended -ExecCmds="Automation RunTests Stopwatch.ClockTower; Quit"
 */
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FClockTowerLayoutTest, "Stopwatch.ClockTower.Layout", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FClockTowerLayoutTest::RunTest(const FString& Parameters)
{
	UWorld* World = FindClockTowerWorld();

	if (!World)
	{
		AddInfo(TEXT("Skipped: Lvl_ClockTower isn't the running game's level."));
		return true;
	}

	APlayerController* Controller = World->GetFirstPlayerController();
	AStopwatchCharacter* Character = Controller ? Controller->GetPawn<AStopwatchCharacter>() : nullptr;

	if (!TestNotNull(TEXT("Player spawned"), Character))
	{
		return true;
	}

	TestTrue(TEXT("Starts at the title screen"), EnsurePlaying(World));

	// a checkpoint and a section trigger at the start of every section
	for (int32 Section = 1; Section <= SectionCount; ++Section)
	{
		const ACheckpoint* Checkpoint = FindActor<ACheckpoint>(World, [Section](const ACheckpoint* Actor) { return Actor->SectionId == Section && Actor->OrderInSection == 0; });
		const ASectionTrigger* Trigger = FindActor<ASectionTrigger>(World, [Section](const ASectionTrigger* Actor) { return Actor->SectionId == Section; });

		if (TestNotNull(FString::Printf(TEXT("Section %d's first checkpoint"), Section), Checkpoint)
			&& TestNotNull(FString::Printf(TEXT("Section %d's trigger"), Section), Trigger))
		{
			TestTrue(FString::Printf(TEXT("Section %d's checkpoint is at its start"), Section), FVector::Dist(Checkpoint->GetActorLocation(), Trigger->GetActorLocation()) < 800.0);
			TestFalse(FString::Printf(TEXT("Section %d has a name"), Section), Trigger->DisplayName.IsEmpty());
		}
	}

	TestTrue(TEXT("Mid-section checkpoints in the long sections"), CountActors<ACheckpoint>(World) >= SectionCount * 2);

	const ACollapseTrigger* Collapse = FindActor<ACollapseTrigger>(World, [](const ACollapseTrigger*) { return true; });
	TestEqual(TEXT("One goal"), CountActors<AGoalTrigger>(World), 1);
	TestEqual(TEXT("One collapse trigger"), CountActors<ACollapseTrigger>(World), 1);
	TestTrue(TEXT("The collapse is 120 s"), Collapse && Collapse->CollapseSeconds == 120.0f);

	// every piece a checkpoint resets belongs to a section
	for (TActorIterator<AActor> It(World); It; ++It)
	{
		if (const IResettable* Resettable = Cast<IResettable>(*It); Resettable && It->GetOwner() == nullptr)
		{
			TestTrue(FString::Printf(TEXT("%s is in a section"), *It->GetActorNameOrLabel()), Resettable->GetSectionId() >= 1 && Resettable->GetSectionId() <= SectionCount);
		}
	}

	// sand everywhere a retry is likely
	for (TActorIterator<AHourglassPickup> It(World); It; ++It)
	{
		TestTrue(FString::Printf(TEXT("%s comes back"), *It->GetActorNameOrLabel()), It->RespawnTime > 0.0f);
	}

	AddInfo(FString::Printf(TEXT("%d checkpoints, %d hourglasses, %d tutorials"), CountActors<ACheckpoint>(World), CountActors<AHourglassPickup>(World), CountActors<ATutorialTrigger>(World)));

	// what each tutorial is about is in view, and in reach, from where it's shown
	const float Reach = Character->GetTimeAbility()->FreezeRange;

	for (TActorIterator<ATutorialTrigger> It(World); It; ++It)
	{
		const AActor* Focus = It->FocusTarget;

		if (!TestNotNull(FString::Printf(TEXT("%s has a focus"), *It->GetActorNameOrLabel()), Focus))
		{
			continue;
		}

		const FVector Eye = It->GetActorLocation() + FVector(0.0f, 0.0f, 160.0f);
		TestTrue(FString::Printf(TEXT("%s's %s is in view"), *It->GetActorNameOrLabel(), *Focus->GetActorNameOrLabel()), IsInView(World, Eye, Focus, Character));
		TestTrue(FString::Printf(TEXT("%s's %s is in reach"), *It->GetActorNameOrLabel(), *Focus->GetActorNameOrLabel()), FVector::Dist(Eye, Focus->GetActorLocation()) < Reach);
		TestTrue(FString::Printf(TEXT("%s names a key"), *It->GetActorNameOrLabel()), It->Prompt.ToString().Contains(TEXT("{freeze}")) || It->Prompt.ToString().Contains(TEXT("{rewind}")));
	}

	// every jump the path needs, against the jump the character really has
	const UCharacterMovementComponent* Movement = Character->GetCharacterMovement();
	const double Up = Movement->JumpZVelocity;
	const double Gravity = -Movement->GetGravityZ();
	const double Speed = Movement->MaxWalkSpeed;
	const double JumpHeight = Up * Up / (2.0 * Gravity);
	constexpr double RiseMargin = 15.0;
	constexpr double DistanceMargin = 0.8;
	int32 Jumps = 0;

	for (TActorIterator<ATargetPoint> From(World); From; ++From)
	{
		if (!From->Tags.Contains(TEXT("JumpFrom")) || From->Tags.Num() < 2)
		{
			continue;
		}

		const FName Id = From->Tags[1];
		const ATargetPoint* To = FindActor<ATargetPoint>(World, [Id](const ATargetPoint* Actor) { return Actor->Tags.Contains(TEXT("JumpTo")) && Actor->Tags.Contains(Id); });

		if (!TestNotNull(FString::Printf(TEXT("%s lands somewhere"), *Id.ToString()), To))
		{
			continue;
		}

		++Jumps;
		const double Rise = To->GetActorLocation().Z - From->GetActorLocation().Z;
		const double Gap = FVector::Dist2D(To->GetActorLocation(), From->GetActorLocation());

		// how far a running jump carries before it comes back down to the landing's height
		const double Discriminant = Up * Up - 2.0 * Gravity * Rise;
		const double Airtime = Discriminant >= 0.0 ? (Up + FMath::Sqrt(Discriminant)) / Gravity : 0.0;
		const double Carry = Speed * Airtime;

		AddInfo(FString::Printf(TEXT("%s: rise %.0f of %.0f, gap %.0f of %.0f"), *Id.ToString(), Rise, JumpHeight, Gap, Carry));
		TestTrue(FString::Printf(TEXT("%s rises less than a jump, with margin"), *Id.ToString()), Rise <= JumpHeight - RiseMargin);
		TestTrue(FString::Printf(TEXT("%s is shorter than a jump, with margin"), *Id.ToString()), Gap <= Carry * DistanceMargin);
	}

	TestTrue(TEXT("The jumps are marked"), Jumps >= 10);

	// the timing windows: the Belfry's chute crate passing the walkway, and its block rising past the step when rewound
	ADropChute* Chute = FindActor<ADropChute>(World, [](const ADropChute* Actor) { return Actor->SectionId == 4; });
	APhysicsProp* Block = FindActor<APhysicsProp>(World, [](const APhysicsProp* Actor) { return Actor->SectionId == 4 && Actor->bHeldUntilWoken; });

	if (!TestNotNull(TEXT("The Belfry's chute"), Chute) || !TestNotNull(TEXT("The Belfry's block"), Block))
	{
		return true;
	}

	struct FState
	{
		int32 Step = 0;
		double StepTime = 0.0;
		double LastTime = 0.0;
		double CrateWindow = 0.0;
		double BlockWindow = 0.0;
		TWeakObjectPtr<APhysicsProp> Crate;
		bool bCrateDone = false;
	};
	TSharedRef<FState> State = MakeShared<FState>();
	State->StepTime = State->LastTime = World->GetTimeSeconds();

	const double Walkway = Chute->GetActorLocation().Z - ChuteHeight;
	const double BlockFloor = Block->GetActorLocation().Z - BlockHeight;
	Block->Wake(FVector(0.0f, 20.0f, 0.0f));

	ADD_LATENT_AUTOMATION_COMMAND(FFunctionLatentCommand([=, this]()
	{
		const double Now = World->GetTimeSeconds();
		const double Delta = Now - State->LastTime;
		State->LastTime = Now;

		// the first crate that falls past, from its release to the pit
		if (!State->bCrateDone)
		{
			if (!State->Crate.IsValid() && !Chute->GetLiveProps().IsEmpty())
			{
				State->Crate = Chute->GetLiveProps().Last();
			}

			if (const APhysicsProp* Crate = State->Crate.Get())
			{
				const double Top = Crate->GetMesh()->Bounds.GetBox().Max.Z - Walkway;
				State->CrateWindow += FMath::Abs(Top) <= CrateBand ? Delta : 0.0;
				State->bCrateDone = Top < -CrateBand * 2.0;
			}
		}

		switch (State->Step)
		{
		case 0:
			// the block lands, and rewinds
			if (Now - State->StepTime < 3.0)
			{
				return false;
			}

			TestTrue(TEXT("The block landed"), Block->GetActorLocation().Z - BlockFloor < 60.0);
			TestTrue(TEXT("The block rewinds"), Block->GetTimeable()->Rewind());
			++State->Step;
			return false;

		case 1:
		{
			const double Top = Block->GetMesh()->Bounds.GetBox().Max.Z - BlockFloor;
			State->BlockWindow += Top >= BlockBandLow && Top <= BlockBandHigh ? Delta : 0.0;

			if (Block->GetTimeable()->IsRewinding() || !State->bCrateDone)
			{
				return Now - State->StepTime > 30.0 ? (AddError(TEXT("Timed out")), true) : false;
			}

			AddInfo(FString::Printf(TEXT("Crate usable for %.2fs, block for %.2fs"), State->CrateWindow, State->BlockWindow));
			TestTrue(TEXT("Freezing a falling crate as a step has a window of 0.6 s"), State->CrateWindow >= MinWindowSeconds);
			TestTrue(TEXT("Freezing the rewinding block as a step has a window of 0.6 s"), State->BlockWindow >= MinWindowSeconds);
			return true;
		}

		default:
			return true;
		}
	}));

	return true;
}

/**
 *  Plays the Workshop and the Broken Bridge with movement input and the real aim, as a player would. Needs the game running the level
 */
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FClockTowerWalkthroughTest, "Stopwatch.ClockTower.Walkthrough", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FClockTowerWalkthroughTest::RunTest(const FString& Parameters)
{
	UWorld* World = FindClockTowerWorld();

	if (!World)
	{
		AddInfo(TEXT("Skipped: Lvl_ClockTower isn't the running game's level."));
		return true;
	}

	EnsurePlaying(World);

	AStopwatchPlayerController* Controller = Cast<AStopwatchPlayerController>(World->GetFirstPlayerController());
	AStopwatchCharacter* Character = Controller ? Controller->GetPawn<AStopwatchCharacter>() : nullptr;
	AStopwatchGameMode* GameMode = World->GetAuthGameMode<AStopwatchGameMode>();

	// found by what they are, as labels only exist in the editor
	AFreezePendulum* BeamPendulum = FindActor<AFreezePendulum>(World, [](const AFreezePendulum* Actor) { return Actor->SectionId == 1 && !Actor->bKillsPlayer; });
	AFreezePendulum* GatePendulum = FindActor<AFreezePendulum>(World, [](const AFreezePendulum* Actor) { return Actor->SectionId == 1 && Actor->bKillsPlayer; });
	ABrokenBridge* Bridge = FindActor<ABrokenBridge>(World, [](const ABrokenBridge* Actor) { return Actor->SectionId == 3; });
	AFallingPlatform* Falling = FindActor<AFallingPlatform>(World, [](const AFallingPlatform* Actor) { return Actor->SectionId == 3; });
	APhysicsProp* Boulder = FindActor<APhysicsProp>(World, [](const APhysicsProp* Actor) { return Actor->SectionId == 3 && Actor->Shape == EPropShape::Sphere; });
	const ACheckpoint* MidWorkshop = FindActor<ACheckpoint>(World, [](const ACheckpoint* Actor) { return Actor->SectionId == 1 && Actor->OrderInSection == 1; });

	if (!TestNotNull(TEXT("Player"), Character) || !TestNotNull(TEXT("Game mode"), GameMode) || !TestNotNull(TEXT("Beam pendulum"), BeamPendulum)
		|| !TestNotNull(TEXT("Gate pendulum"), GatePendulum) || !TestNotNull(TEXT("Bridge"), Bridge) || !TestNotNull(TEXT("Falling platform"), Falling)
		|| !TestNotNull(TEXT("Boulder"), Boulder) || !TestNotNull(TEXT("Workshop's second checkpoint"), MidWorkshop))
	{
		return true;
	}

	// both sections run along +X
	const FVector Forward(1.0f, 0.0f, 0.0f);
	const FVector BoulderStart = Boulder->GetActorLocation();

	struct FState
	{
		int32 Step = 0;
		double StepTime = 0.0;
	};
	TSharedRef<FState> State = MakeShared<FState>();
	State->StepTime = World->GetTimeSeconds();

	ADD_LATENT_AUTOMATION_COMMAND(FFunctionLatentCommand([=, this]()
	{
		const double Now = World->GetTimeSeconds();
		const double SinceStep = Now - State->StepTime;
		UTimeAbilityComponent* Ability = Character->GetTimeAbility();
		const FVector Location = Character->GetActorLocation();

		auto NextStep = [State, Now]()
		{
			++State->Step;
			State->StepTime = Now;
		};

		auto Fail = [this, State, Character](const FString& What)
		{
			AddError(FString::Printf(TEXT("%s, at %s"), *What, *Character->GetActorLocation().ToString()));
			State->Step = 100;
			return true;
		};

		// stands the player on the floor at a point of the walkway, facing along the path
		auto Stand = [Character, Controller](const FVector& Feet)
		{
			Character->SetActorLocation(Feet + FVector(0.0f, 0.0f, 98.0f), false, nullptr, ETeleportType::TeleportPhysics);
			Character->GetCharacterMovement()->StopMovementImmediately();
			Controller->SetControlRotation(FRotator::ZeroRotator);
		};

		auto Walk = [Character, Forward](float Direction = 1.0f) { Character->AddMovementInput(Forward * Direction, 1.0f); };

		auto Bob = [](const AFreezePendulum* Pendulum) { return FindPart(Pendulum, TEXT("Bob"))->GetComponentLocation(); };

		if (Character->IsDying() && State->Step < 100)
		{
			return Fail(FString::Printf(TEXT("Died at step %d, at %s"), State->Step, *Location.ToString()));
		}

		switch (State->Step)
		{
		// the Workshop: the beam
		case 0:
			Stand(FVector(2450.0f, 0.0f, 0.0f));
			NextStep();
			return false;

		case 1:
			// tracks the swinging bob, and freezes it out to the side, clear of the beam, as the tutorial says
			AimAt(Controller, Bob(BeamPendulum));

			if (SinceStep > 0.2 && FMath::Abs(Bob(BeamPendulum).Y) > 200.0 && Ability->TryFreeze())
			{
				TestTrue(TEXT("The freeze lands on the beam pendulum"), BeamPendulum->GetTimeable()->IsFrozen());
				Controller->SetControlRotation(FRotator::ZeroRotator);
				NextStep();
			}

			return SinceStep > 4.0 ? Fail(TEXT("Couldn't freeze the beam pendulum")) : false;

		case 2:
			// along the beam: it's 200 wide, so the walk keeps to its middle
			Walk();

			if (Location.X > 3400.0f)
			{
				TestTrue(TEXT("Crossed on the beam, not the ledge below"), Location.Z > 50.0f);
				NextStep();
			}

			return SinceStep > 4.0 ? Fail(TEXT("Didn't cross the beam")) : false;

		case 3:
			Walk();

			if (Location.X > 3700.0f)
			{
				TestTrue(TEXT("Walking past reaches the second checkpoint"), GameMode->GetCurrentCheckpoint() == MidWorkshop);
				TestTrue(TEXT("In the Workshop"), GameMode->GetCurrentSectionId() == 1);

				Stand(FVector(3900.0f, 0.0f, 0.0f));
				NextStep();
			}

			return SinceStep > 3.0 ? Fail(TEXT("Didn't walk on to the gate")) : false;

		case 4:
			AimAt(Controller, Bob(GatePendulum));

			if (SinceStep > 0.2 && Ability->TryFreeze())
			{
				// the doorway's side away from the frozen bob
				Stand(FVector(3900.0f, Bob(GatePendulum).Y > 0.0 ? -100.0f : 100.0f, 0.0f));
				NextStep();
			}

			return SinceStep > 4.0 ? Fail(TEXT("Couldn't freeze the gate")) : false;

		case 5:
			Walk();

			if (Location.X > 4600.0f)
			{
				TestTrue(TEXT("Through the gate while it's frozen"), GatePendulum->GetTimeable()->IsFrozen());
				NextStep();
			}

			return SinceStep > 4.0 ? Fail(TEXT("Didn't get through the gate")) : false;

		// the Broken Bridge, at the edge of the pit. The test skips the sand the hourglasses would give on the way
		case 6:
			Character->GetTimeSand()->SetSand(Character->GetTimeSand()->MaxSand);
			Stand(FVector(950.0f, 0.0f, 3000.0f));
			NextStep();
			return false;

		case 7:
		{
			// aims at the nearest plank the view reaches
			if (SinceStep < 0.2)
			{
				return false;
			}

			// the nearest one under the edge is hidden by the walkway's lip
			const FVector Eye = Controller->PlayerCameraManager->GetCameraLocation();
			const UStaticMeshComponent* Plank = nullptr;

			for (const UStaticMeshComponent* Candidate : Bridge->GetPlanks())
			{
				FHitResult Hit;
				FCollisionQueryParams Params(SCENE_QUERY_STAT(PlankView), false, Character);
				const bool bInView = World->LineTraceSingleByChannel(Hit, Eye, Candidate->GetComponentLocation(), ECC_Visibility, Params) && Hit.GetActor() == Bridge;

				if (bInView && (!Plank || FVector::Dist(Candidate->GetComponentLocation(), Eye) < FVector::Dist(Plank->GetComponentLocation(), Eye)))
				{
					Plank = Candidate;
				}
			}

			if (!Plank)
			{
				return Fail(TEXT("No plank in view from the edge"));
			}

			AimAt(Controller, Plank->GetComponentLocation());

			if (SinceStep > 0.4)
			{
				TestEqual(TEXT("The planks are the aim target"), Ability->GetAimTarget(), Bridge->GetTimeable());
				TestTrue(TEXT("Rewind the bridge"), Ability->TryRewind());
				NextStep();
			}

			return false;
		}

		case 8:
			if (Bridge->GetTimeable()->IsRewinding())
			{
				return SinceStep > 5.0 ? Fail(TEXT("The bridge never finished rewinding")) : false;
			}

			Controller->SetControlRotation(FRotator::ZeroRotator);
			Walk();

			if (Location.X > 2450.0f)
			{
				TestTrue(TEXT("The bridge holds"), Bridge->GetTimeable()->IsHolding());
				NextStep();
			}

			return SinceStep > 8.0 ? Fail(TEXT("Didn't cross the bridge")) : false;

		// the falling platform
		case 9:
			Walk();

			if (Falling->CaptureTimeState() > 0.0f)
			{
				NextStep();
			}

			return SinceStep > 5.0 ? Fail(TEXT("Walking onto the falling platform didn't start the shake")) : false;

		case 10:
			Walk(-1.0f);

			if (Falling->GetMesh()->IsSimulatingPhysics())
			{
				TestTrue(TEXT("Back on the near floor before it dropped"), Location.X < 3000.0f);
				NextStep();
			}

			return SinceStep > 3.0 ? Fail(TEXT("The falling platform didn't drop")) : false;

		case 11:
			if (SinceStep < 2.5)
			{
				return false;
			}

			Stand(FVector(2950.0f, 0.0f, 3000.0f));
			NextStep();
			return false;

		case 12:
			AimAt(Controller, Falling->GetActorLocation());

			if (SinceStep > 0.3)
			{
				TestEqual(TEXT("The fallen platform is the aim target from the edge"), Ability->GetAimTarget(), Falling->GetTimeable());
				TestTrue(TEXT("Rewind the platform"), Ability->TryRewind());
				NextStep();
			}

			return false;

		case 13:
			if (Falling->GetTimeable()->IsRewinding())
			{
				return SinceStep > 6.0 ? Fail(TEXT("The platform never finished rewinding")) : false;
			}

			Controller->SetControlRotation(FRotator::ZeroRotator);
			Walk();

			if (Location.X > 4200.0f)
			{
				TestTrue(TEXT("The platform held"), Falling->GetTimeable()->IsHolding());
				NextStep();
			}

			return SinceStep > 6.0 ? Fail(TEXT("Didn't cross the falling platform")) : false;

		// the boulder: through the pass and over the plate
		case 14:
			// again the sand of the hourglasses beside the path
			Character->GetTimeSand()->SetSand(Character->GetTimeSand()->MaxSand);
			Stand(FVector(4250.0f, -300.0f, 3000.0f));
			NextStep();
			return false;

		case 15:
			Walk();

			if (Location.X > 5000.0f)
			{
				NextStep();
			}

			return SinceStep > 4.0 ? Fail(TEXT("Didn't walk through the pass")) : false;

		case 16:
		{
			// waits for the boulder to come to rest, then checks it jammed the door
			const bool bResting = Boulder->GetMesh()->GetPhysicsLinearVelocity().Size() < 5.0f;

			if (SinceStep < 2.0 || (!bResting && SinceStep < 8.0))
			{
				return false;
			}

			const FVector Door(5700.0f, 300.0f, 3000.0f);
			AddInfo(FString::Printf(TEXT("Boulder rests at %s"), *Boulder->GetActorLocation().ToString()));
			TestTrue(TEXT("The plate rolled the boulder"), FVector::Dist(Boulder->GetActorLocation(), BoulderStart) > 500.0);
			TestTrue(TEXT("The boulder jams the door"), FVector::Dist2D(Boulder->GetActorLocation(), Door) < 250.0);
			Stand(FVector(5450.0f, 0.0f, 3000.0f));
			NextStep();
			return false;
		}

		case 17:
			AimAt(Controller, Boulder->GetActorLocation());

			if (SinceStep > 0.3)
			{
				TestTrue(TEXT("Rewind the boulder"), Ability->TryRewind());
				NextStep();
			}

			return false;

		case 18:
			if (Boulder->GetTimeable()->IsRewinding())
			{
				return SinceStep > 12.0 ? Fail(TEXT("The boulder never finished rewinding")) : false;
			}

			if (SinceStep < 1.0)
			{
				return false;
			}

			TestTrue(TEXT("The boulder went back up the ramp"), FVector::Dist(Boulder->GetActorLocation(), BoulderStart) < 50.0);
			Stand(FVector(5450.0f, 300.0f, 3000.0f));
			NextStep();
			return false;

		case 19:
			Walk();

			if (Location.X > 5900.0f)
			{
				NextStep();
				return true;
			}

			return SinceStep > 4.0 ? Fail(TEXT("Didn't get through the door")) : false;

		default:
			return true;
		}
	}));

	return true;
}

#endif
