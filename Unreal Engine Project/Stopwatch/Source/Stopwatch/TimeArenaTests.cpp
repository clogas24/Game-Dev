// Copyright Epic Games, Inc. All Rights Reserved.

#include "BrokenBridge.h"
#include "Components/StaticMeshComponent.h"
#include "Crusher.h"
#include "DropChute.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "FallingPlatform.h"
#include "FreezePendulum.h"
#include "Camera/CameraComponent.h"
#include "Camera/PlayerCameraManager.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/PlayerStart.h"
#include "KillVolume.h"
#include "Misc/AutomationTest.h"
#include "MovingPlatform.h"
#include "PhysicsCrate.h"
#include "SpinningGear.h"
#include "StopwatchCharacter.h"
#include "StopwatchHUDWidget.h"
#include "StopwatchPlayerController.h"
#include "TimeAbilityComponent.h"
#include "TimeableComponent.h"
#include "TimeSandComponent.h"
#include "Tests/AutomationCommon.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace
{
	template <typename T, typename TPredicate>
	T* FindActor(UWorld* World, TPredicate Predicate)
	{
		for (TActorIterator<T> It(World); It; ++It)
		{
			if (Predicate(*It))
			{
				return *It;
			}
		}

		return nullptr;
	}

	template <typename T>
	int32 CountActors(UWorld* World)
	{
		int32 Count = 0;

		for (TActorIterator<T> It(World); It; ++It)
		{
			++Count;
		}

		return Count;
	}
}

/**
 *  Plays the saved arena, so needs the game running it:
 *  UnrealEditor-Cmd.exe Stopwatch.uproject /Game/Stopwatch/Maps/Lvl_TimeTest -game -nullrhi -unattended -ExecCmds="Automation RunTests Stopwatch.Arena; Quit"
 */
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTimeArenaTest, "Stopwatch.Arena.Play", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FTimeArenaTest::RunTest(const FString& Parameters)
{
	UWorld* World = nullptr;

	for (const FWorldContext& Context : GEngine->GetWorldContexts())
	{
		if (Context.WorldType == EWorldType::Game && Context.World() && Context.World()->GetName() == TEXT("Lvl_TimeTest"))
		{
			World = Context.World();
		}
	}

	if (!World)
	{
		AddInfo(TEXT("Skipped: Lvl_TimeTest isn't the running game's level."));
		return true;
	}

	APlayerController* Controller = World->GetFirstPlayerController();
	AStopwatchCharacter* Character = Controller ? Controller->GetPawn<AStopwatchCharacter>() : nullptr;

	if (!TestNotNull(TEXT("Player spawned"), Character))
	{
		return true;
	}

	// what was saved survived the save
	TestEqual(TEXT("One PlayerStart"), CountActors<APlayerStart>(World), 1);
	TestEqual(TEXT("One kill volume"), CountActors<AKillVolume>(World), 1);
	TestEqual(TEXT("Three pendulums"), CountActors<AFreezePendulum>(World), 3);
	TestEqual(TEXT("Two bridges"), CountActors<ABrokenBridge>(World), 2);
	TestEqual(TEXT("Two crushers"), CountActors<ACrusher>(World), 2);
	TestEqual(TEXT("Two gears"), CountActors<ASpinningGear>(World), 2);

	AFreezePendulum* Blade = FindActor<AFreezePendulum>(World, [](const AFreezePendulum* Actor) { return Actor->bKillsPlayer; });
	ABrokenBridge* SafeBridge = FindActor<ABrokenBridge>(World, [](const ABrokenBridge* Actor) { return Actor->GetTimeable()->HoldDuration == 0.0f; });
	ABrokenBridge* TimedBridge = FindActor<ABrokenBridge>(World, [](const ABrokenBridge* Actor) { return Actor->GetTimeable()->HoldDuration == 4.0f; });
	APhysicsCrate* Crate = FindActor<APhysicsCrate>(World, [](const APhysicsCrate* Actor) { return Actor->AutoResetInterval == 4.0f; });
	APhysicsProp* Boulder = FindActor<APhysicsProp>(World, [](const APhysicsProp* Actor) { return Actor->Shape == EPropShape::Sphere && Actor->bHeldUntilWoken; });
	AFallingPlatform* Falling = FindActor<AFallingPlatform>(World, [](const AFallingPlatform*) { return true; });
	ACrusher* Crusher = FindActor<ACrusher>(World, [](const ACrusher*) { return true; });
	ADropChute* Chute = FindActor<ADropChute>(World, [](const ADropChute*) { return true; });
	ASpinningGear* BladeGear = FindActor<ASpinningGear>(World, [](const ASpinningGear* Actor) { return Actor->bIsBlade; });
	APhysicsProp* Block = FindActor<APhysicsProp>(World, [](const APhysicsProp* Actor) { return Actor->Shape == EPropShape::Cube && Actor->bHeldUntilWoken; });

	if (!TestNotNull(TEXT("Blade pendulum"), Blade) || !TestNotNull(TEXT("Safe bridge"), SafeBridge) || !TestNotNull(TEXT("Timed bridge"), TimedBridge)
		|| !TestNotNull(TEXT("Crate"), Crate) || !TestNotNull(TEXT("Boulder"), Boulder) || !TestNotNull(TEXT("Falling platform"), Falling)
		|| !TestNotNull(TEXT("Crusher"), Crusher) || !TestNotNull(TEXT("Chute"), Chute) || !TestNotNull(TEXT("Blade gear"), BladeGear) || !TestNotNull(TEXT("Block"), Block))
	{
		return true;
	}

	TestTrue(TEXT("Bridge planks exist at play time"), SafeBridge->GetPlanks().Num() >= 8);
	TestTrue(TEXT("Timed bridge takes longer to cross than it holds"), TimedBridge->BridgeLength / 500.0f > TimedBridge->GetTimeable()->HoldDuration);
	TestFalse(TEXT("A boulder waiting on its ramp isn't simulating"), Boulder->GetMesh()->IsSimulatingPhysics());
	TestEqual(TEXT("Crusher's saved head size"), Crusher->HeadSize.X, 460.0);

	// the script's heights and distances assume these, and the blueprint could override the C++ defaults
	const UCharacterMovementComponent* Movement = Character->GetCharacterMovement();
	const double JumpHeight = FMath::Square(Movement->JumpZVelocity) / (2.0 * -Movement->GetGravityZ());
	AddInfo(FString::Printf(TEXT("Jump height %.0f, walk speed %.0f"), JumpHeight, Movement->MaxWalkSpeed));
	TestTrue(TEXT("Jump height the arena was built for"), FMath::IsNearlyEqual(JumpHeight, 127.0, 5.0));
	TestEqual(TEXT("Walk speed the arena was built for"), Movement->MaxWalkSpeed, 500.0f);

	// the blueprint could override these too
	TestTrue(TEXT("Starts in first person"), Character->bFirstPerson);
	TestTrue(TEXT("First person camera active"), Character->GetFirstPersonCamera()->IsActive());
	TestFalse(TEXT("Third person camera inactive"), Character->GetFollowCamera()->IsActive());
	TestEqual(TEXT("Field of view"), Character->GetFirstPersonCamera()->FieldOfView, Character->FirstPersonFOV);
	TestTrue(TEXT("Body faces where the player looks"), Character->bUseControllerRotationYaw && !Movement->bOrientRotationToMovement);
	TestTrue(TEXT("Own body hidden"), Character->GetMesh()->bOwnerNoSee);
	TestTrue(TEXT("Hidden body still casts a shadow"), Character->GetMesh()->bCastHiddenShadow && Character->GetMesh()->CastShadow);
	TestEqual(TEXT("Pitch floor"), Controller->PlayerCameraManager->ViewPitchMin, -85.0f);
	TestEqual(TEXT("Pitch ceiling"), Controller->PlayerCameraManager->ViewPitchMax, 85.0f);

	// the capsule keeps walls and crusher heads this far from the eye, and a 10 unit near plane reaches 16 at its corners with a 90 degree view
	const double EyeHeadroom = Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight() - Character->GetFirstPersonCamera()->GetRelativeLocation().Z;
	TestTrue(TEXT("Nothing that blocks the capsule clips the near plane"), EyeHeadroom > 16.0 && Character->GetCapsuleComponent()->GetScaledCapsuleRadius() > 16.0);

	struct FState
	{
		int32 Step = 0;
		double StepTime = 0.0;
		FVector StartLocation = FVector::ZeroVector;
		FVector CrateStart = FVector::ZeroVector;
		float BoulderZ = 0.0f;
		FVector BlockStart = FVector::ZeroVector;
		double BlockWindowSeconds = 0.0;
		double LastTime = 0.0;
		bool bPlacedInPit = false;
	};
	TSharedRef<FState> State = MakeShared<FState>();
	State->StepTime = World->GetTimeSeconds();
	State->CrateStart = Crate->GetActorLocation();
	State->BoulderZ = Boulder->GetActorLocation().Z;
	State->BlockStart = Block->GetActorLocation();

	// Hazards kill on their next tick, the fade takes DeathFadeSeconds, and the pieces move in world time, so steps wait on the clock
	ADD_LATENT_AUTOMATION_COMMAND(FFunctionLatentCommand([=, this]()
	{
		const double Now = World->GetTimeSeconds();
		const double SinceStep = Now - State->StepTime;
		UTimeAbilityComponent* Ability = Character->GetTimeAbility();
		UTimeSandComponent* Sand = Character->GetTimeSand();

		auto NextStep = [State, Now]()
		{
			++State->Step;
			State->StepTime = Now;
		};

		// stands the player somewhere and keeps them there
		auto Place = [Character](const FVector& Location)
		{
			Character->SetActorLocation(Location, false, nullptr, ETeleportType::TeleportPhysics);
		};

		// waits out a death from whichever hazard the player was just put in
		auto ExpectDeath = [this, Character, &NextStep, SinceStep](const TCHAR* What)
		{
			if (Character->IsDying())
			{
				NextStep();
			}
			else if (SinceStep > 3.0)
			{
				AddError(FString::Printf(TEXT("%s didn't kill the player"), What));
				NextStep();
			}
		};

		switch (State->Step)
		{
		case 0:
			if (SinceStep < 1.0)
			{
				return false;
			}

			TestFalse(TEXT("The player survives the spawn"), Character->IsDying());
			TestTrue(TEXT("The player stands on the hub"), FMath::Abs(Character->GetActorLocation().Z - 96.0) < 40.0);
			State->StartLocation = Character->GetActorLocation();

			TestTrue(TEXT("Fallen planks are out of reach, under the kill volume"), SafeBridge->GetPlanks()[0]->GetComponentLocation().Z < -300.0);

			// a rewind with nothing to go back to is free
			TestFalse(TEXT("A waiting boulder can't be rewound"), Ability->TryRewindTarget(Boulder->GetTimeable()));
			TestEqual(TEXT("Refused rewinds are free"), Sand->GetCurrentSand(), 100.0f);

			TestTrue(TEXT("Safe bridge rewinds"), Ability->TryRewindTarget(SafeBridge->GetTimeable()));
			TestEqual(TEXT("Rewind costs 35"), Sand->GetCurrentSand(), 65.0f);
			TimedBridge->GetTimeable()->Rewind();
			Falling->StartShaking();
			// what its trigger does
			Boulder->Wake(FVector(0.0f, -100.0f, 0.0f));
			NextStep();
			return false;

		case 1:
			if (SinceStep < 2.5)
			{
				return false;
			}

			TestTrue(TEXT("Bridge restored, with its top level with the floor"), FMath::IsNearlyEqual(SafeBridge->GetPlanks()[0]->GetComponentLocation().Z, -14.0, 1.0));
			TestTrue(TEXT("Falling platform is dropping"), Falling->GetMesh()->IsSimulatingPhysics());
			AddInfo(FString::Printf(TEXT("Boulder from Z %.0f to %s, velocity %s, simulating %d, awake %d"), State->BoulderZ, *Boulder->GetActorLocation().ToString(), *Boulder->GetMesh()->GetPhysicsLinearVelocity().ToString(), Boulder->GetMesh()->IsSimulatingPhysics(), Boulder->GetMesh()->IsAnyRigidBodyAwake()));
			TestTrue(TEXT("Boulder rolled down the ramp"), Boulder->GetActorLocation().Z < State->BoulderZ - 50.0f);
			TestTrue(TEXT("Chute released something"), !Chute->GetLiveProps().IsEmpty());
			Blade->RestoreTimeState(Blade->Period * 0.5f);
			NextStep();
			return false;

		case 2:
			// a blade at the bottom of its swing: the kill zone is around the bob
			Place(Blade->GetActorLocation() - FVector(0.0f, 0.0f, Blade->ArmLength));
			ExpectDeath(TEXT("Blade pendulum"));
			return false;

		case 3:
			if (Character->IsDying())
			{
				return false;
			}

			TestTrue(TEXT("Respawned at the PlayerStart"), FVector::Dist2D(Character->GetActorLocation(), State->StartLocation) < 100.0);
			TestEqual(TEXT("Respawn refills the sand"), Sand->GetCurrentSand(), Sand->MaxSand);
			NextStep();
			return false;

		case 4:
			// stands on the floor under the crusher, and a slam comes around within a cycle
			Place(Crusher->GetActorLocation() + FVector(0.0f, 0.0f, 100.0f));
			ExpectDeath(TEXT("Crusher"));
			return false;

		case 5:
			if (!Character->IsDying())
			{
				NextStep();
			}

			return false;

		case 6:
			// the arm sweeps everything within its radius at this height
			Place(BladeGear->GetActorLocation() + BladeGear->GetActorRotation().RotateVector(FVector(BladeGear->Radius * 0.7f, 0.0f, 0.0f)));
			ExpectDeath(TEXT("Blade gear"));
			return false;

		case 7:
			if (!Character->IsDying())
			{
				NextStep();
			}

			return false;

		case 8:
			// falls down an empty pit, so only gets placed the once
			if (!State->bPlacedInPit)
			{
				State->bPlacedInPit = true;
				Place(FVector(Falling->GetActorLocation().X + 2000.0, Falling->GetActorLocation().Y, -200.0));
			}

			ExpectDeath(TEXT("Kill volume"));
			return false;

		case 9:
			if (!Character->IsDying())
			{
				NextStep();
			}

			return false;

		case 10:
		{
			// aiming for real, through the camera: a crate's freezable, and the crosshair knows
			const FVector Target = Crate->GetActorLocation();
			Place(Target + FVector(-500.0f, 0.0f, 0.0f));
			Controller->SetControlRotation((Target - Controller->PlayerCameraManager->GetCameraLocation()).Rotation());

			if (SinceStep > 0.5)
			{
				const TCHAR* View = Character->bFirstPerson ? TEXT("first person") : TEXT("third person");
				const UCameraComponent* Camera = Character->bFirstPerson ? Character->GetFirstPersonCamera() : Character->GetFollowCamera();
				TestTrue(FString::Printf(TEXT("View comes from the %s camera"), View), Controller->PlayerCameraManager->GetCameraLocation().Equals(Camera->GetComponentLocation(), 1.0));
				TestEqual(FString::Printf(TEXT("Aim state on a crate, %s"), View), (int32)Ability->GetAimState(), (int32)ETimeAimState::CanFreeze);
				TestEqual(FString::Printf(TEXT("Aim target is the crate, %s"), View), Ability->GetAimTarget(), Crate->GetTimeable());
				TestTrue(FString::Printf(TEXT("Body turned to the aim, %s"), View), FMath::IsNearlyZero(FRotator::NormalizeAxis(Character->GetActorRotation().Yaw - Controller->GetControlRotation().Yaw), 1.0f));

				// again from behind the player, whose own body is now in the way of the trace
				if (Character->bFirstPerson)
				{
					Character->SetFirstPerson(false);
					TestFalse(TEXT("Body visible from outside"), Character->GetMesh()->bOwnerNoSee);
					State->StepTime = Now;
					return false;
				}

				Character->SetFirstPerson(true);

				// station 9: what its trigger does
				Block->Wake(FVector(-20.0f, 0.0f, 0.0f));
				NextStep();
			}

			return false;
		}

		case 11:
		{
			if (SinceStep < 3.0)
			{
				return false;
			}

			// the block must land in front of the step, which is centered 10 from where it started
			const FVector Location = Block->GetActorLocation();
			const FRotator Rotation = Block->GetActorRotation();
			const double StepCenterX = State->BlockStart.X + 10.0;
			AddInfo(FString::Printf(TEXT("Block landed at %s, rotation %s"), *Location.ToString(), *Rotation.ToString()));
			TestTrue(TEXT("Block landed in front of the step"), FMath::Abs(Location.X - StepCenterX) < 150.0);
			TestTrue(TEXT("Block landed flat, 60 units tall"), FMath::IsNearlyEqual(Location.Z, 30.0, 10.0));
			TestTrue(TEXT("Block didn't tumble"), FMath::Abs(Rotation.Pitch) < 5.0 && FMath::Abs(Rotation.Roll) < 5.0);

			TestTrue(TEXT("Block rewinds"), Block->GetTimeable()->Rewind());
			State->LastTime = Now;
			NextStep();
			return false;
		}

		case 12:
		{
			// how long the player has to freeze it: from a jump off the floor's step, and to a jump to the raised platform
			const double Top = Block->GetActorLocation().Z + 30.0;

			if (Top >= 140.0 && Top <= 220.0)
			{
				State->BlockWindowSeconds += Now - State->LastTime;
			}

			State->LastTime = Now;

			if (Block->GetTimeable()->IsRewinding())
			{
				return false;
			}

			AddInfo(FString::Printf(TEXT("Block's freeze window %.2fs"), State->BlockWindowSeconds));
			TestTrue(TEXT("Block's freeze window is humanly possible"), State->BlockWindowSeconds >= 0.25);
			TestTrue(TEXT("Block went back to its ledge"), FVector::Dist(Block->GetActorLocation(), State->BlockStart) < 20.0);

			TestTrue(TEXT("Boulder rewinds"), Boulder->GetTimeable()->Rewind());
			NextStep();
			return false;
		}

		case 13:
			if (Boulder->GetTimeable()->IsRewinding())
			{
				return false;
			}

			TestTrue(TEXT("Boulder went back up"), FMath::IsNearlyEqual(Boulder->GetActorLocation().Z, State->BoulderZ, 5.0));
			TestTrue(TEXT("Physics back after the rewind"), Boulder->GetMesh()->IsSimulatingPhysics());
			NextStep();
			return false;

		case 14:
			if (SinceStep < 2.0)
			{
				return false;
			}

			// a body restored at rest on the slope goes to sleep, which is what the station's label says will happen
			TestTrue(TEXT("Boulder stays up there"), FMath::IsNearlyEqual(Boulder->GetActorLocation().Z, State->BoulderZ, 5.0));

			// walking into the trigger again
			Boulder->Wake(FVector(0.0f, -100.0f, 0.0f));
			NextStep();
			return false;

		case 15:
			if (SinceStep < 2.0)
			{
				return false;
			}

			TestTrue(TEXT("The trigger rolls it again"), Boulder->GetActorLocation().Z < State->BoulderZ - 50.0f);
			NextStep();
			return false;

		case 16:
		{
			// waits out any pop from the aim state changing, so the next one can only come from the freeze
			if (SinceStep < 0.3)
			{
				return false;
			}

			const UStopwatchHUDWidget* HUD = Cast<AStopwatchPlayerController>(Controller)->GetHUDWidget();
			TestEqual(TEXT("Dot at rest"), HUD->GetDotScale(), 1.0f);
			Sand->SetSand(Sand->MaxSand);
			TestTrue(TEXT("Freeze for the pop"), Ability->TryFreezeTarget(Blade->GetTimeable()));
			TestEqual(TEXT("Successful spend pops the dot"), HUD->GetDotScale(), HUD->PopScale);
			NextStep();
			return false;
		}

		case 17:
		{
			if (SinceStep < 0.3)
			{
				return false;
			}

			const UStopwatchHUDWidget* HUD = Cast<AStopwatchPlayerController>(Controller)->GetHUDWidget();
			TestEqual(TEXT("Pop ended"), HUD->GetDotScale(), 1.0f);
			Sand->SetSand(0.0f);
			TestFalse(TEXT("Can't pay"), Ability->TryFreezeTarget(Blade->GetTimeable()));
			TestTrue(TEXT("Failed spend tints the dot red"), HUD->GetDotColor().R > 0.8f && HUD->GetDotColor().G < 0.1f);
			NextStep();
			return false;
		}

		case 18:
			if (SinceStep < 0.5)
			{
				return false;
			}

			TestTrue(TEXT("The red tint ends"), Cast<AStopwatchPlayerController>(Controller)->GetHUDWidget()->GetDotColor().G > 0.1f);
			Sand->SetSand(Sand->MaxSand);

			// the settings menu pauses, and saves the sensitivity when it closes
			{
				AStopwatchPlayerController* StopwatchController = Cast<AStopwatchPlayerController>(Controller);
				const float Original = StopwatchController->GetMouseSensitivity();

				StopwatchController->ToggleSettingsMenu();
				TestTrue(TEXT("Menu open"), StopwatchController->IsSettingsMenuOpen());
				TestTrue(TEXT("Menu pauses the game"), World->IsPaused());

				StopwatchController->SetMouseSensitivity(100.0f);
				TestEqual(TEXT("Sensitivity clamped to the menu's range"), StopwatchController->GetMouseSensitivity(), 3.0f);
				StopwatchController->SetMouseSensitivity(2.0f);

				StopwatchController->ToggleSettingsMenu();
				TestFalse(TEXT("Menu closed"), StopwatchController->IsSettingsMenuOpen());
				TestFalse(TEXT("Game resumed"), World->IsPaused());

				float Saved = 0.0f;
				GConfig->GetFloat(TEXT("Stopwatch.Settings"), TEXT("MouseSensitivity"), Saved, GGameUserSettingsIni);
				TestEqual(TEXT("Sensitivity saved"), Saved, 2.0f);

				// puts the player's own setting back
				StopwatchController->ToggleSettingsMenu();
				StopwatchController->SetMouseSensitivity(Original);
				StopwatchController->ToggleSettingsMenu();
			}

			return true;
		}

		return true;
	}));

	return true;
}

#endif
