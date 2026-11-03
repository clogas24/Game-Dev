// Copyright Epic Games, Inc. All Rights Reserved.

#include "BrokenBridge.h"
#include "Components/StaticMeshComponent.h"
#include "Crusher.h"
#include "DropChute.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "FallingPlatform.h"
#include "FreezePendulum.h"
#include "GameFramework/PlayerInput.h"
#include "GameFramework/PlayerStart.h"
#include "Misc/AutomationTest.h"
#include "MovingPlatform.h"
#include "PhysicsCrate.h"
#include "StopwatchCharacter.h"
#include "TimeAbilityComponent.h"
#include "TimeableComponent.h"
#include "TimeSandComponent.h"
#include "Tests/AutomationCommon.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace
{
	constexpr float TickSeconds = 0.05f;

	UWorld* MakeTestWorld()
	{
		UWorld* World = UWorld::CreateWorld(EWorldType::Game, false);
		FWorldContext& Context = GEngine->CreateNewWorldContext(EWorldType::Game);
		Context.SetCurrentWorld(World);
		World->InitializeActorsForPlay(FURL());
		World->SetBegunPlay(true);
		return World;
	}

	void DestroyTestWorld(UWorld* World)
	{
		GEngine->DestroyWorldContext(World);
		World->DestroyWorld(false);
	}

	template <typename T>
	T* SpawnStarted(UWorld* World, const FVector& Location = FVector::ZeroVector)
	{
		T* Actor = World->SpawnActor<T>(Location, FRotator::ZeroRotator);
		Actor->DispatchBeginPlay();
		return Actor;
	}
}

// Run with: Automation RunTests Stopwatch
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FRewindRecordedTest, "Stopwatch.Rewind.Recorded", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FRewindRecordedTest::RunTest(const FString& Parameters)
{
	UWorld* World = MakeTestWorld();

	// no floor, so the crates keep falling and always have a velocity
	const FVector Start(0.0f, 0.0f, 10000.0f);
	APhysicsCrate* Plain = SpawnStarted<APhysicsCrate>(World, Start);
	APhysicsCrate* Held = World->SpawnActor<APhysicsCrate>(Start + FVector(1000.0f, 0.0f, 0.0f), FRotator::ZeroRotator);
	Held->GetTimeable()->bHoldAfterRewind = true;
	Held->GetTimeable()->HoldDuration = 1.0f;
	Held->DispatchBeginPlay();
	APhysicsCrate* Interrupted = SpawnStarted<APhysicsCrate>(World, Start + FVector(2000.0f, 0.0f, 0.0f));
	APhysicsCrate* ShortBuffer = World->SpawnActor<APhysicsCrate>(Start + FVector(3000.0f, 0.0f, 0.0f), FRotator::ZeroRotator);
	ShortBuffer->GetTimeable()->RecordSeconds = 0.5f;
	ShortBuffer->DispatchBeginPlay();
	AFreezePendulum* Pendulum = SpawnStarted<AFreezePendulum>(World, FVector(0.0f, 5000.0f, 0.0f));

	// never moves: nothing to rewind
	AFallingPlatform* Still = SpawnStarted<AFallingPlatform>(World, FVector(0.0f, -5000.0f, 0.0f));

	AActor* Player = World->SpawnActor<AActor>();
	UTimeSandComponent* Sand = NewObject<UTimeSandComponent>(Player);
	UTimeAbilityComponent* Ability = NewObject<UTimeAbilityComponent>(Player);
	Player->AddInstanceComponent(Sand);
	Player->AddInstanceComponent(Ability);
	Sand->RegisterComponent();
	Ability->RegisterComponent();

	struct FState
	{
		int32 Frame = 0;
		FVector FrozenLocation = FVector::ZeroVector;
		float PendulumClock = 0.0f;
		int32 RewindEvents = 0;
	};
	TSharedRef<FState> State = MakeShared<FState>();

	ADD_LATENT_AUTOMATION_COMMAND(FFunctionLatentCommand([=, this]()
	{
		World->Tick(LEVELTICK_All, TickSeconds);
		const int32 Frame = ++State->Frame;

		if (Frame == 20)
		{
			// paying
			TestFalse(TEXT("Still platform has nothing to rewind"), Still->GetTimeable()->CanBeRewound());
			TestFalse(TEXT("Rewind refused on a still platform"), Ability->TryRewindTarget(Still->GetTimeable()));
			TestFalse(TEXT("No target"), Ability->TryRewindTarget(nullptr));
			TestEqual(TEXT("Refused rewinds are free"), Sand->GetCurrentSand(), 100.0f);

			Sand->SetSand(34.0f);
			TestFalse(TEXT("Unaffordable"), Ability->TryRewindTarget(Plain->GetTimeable()));
			TestFalse(TEXT("Unaffordable doesn't start"), Plain->GetTimeable()->IsRewinding());
			TestEqual(TEXT("Unaffordable isn't charged"), Sand->GetCurrentSand(), 34.0f);

			Sand->SetSand(100.0f);
			TestTrue(TEXT("Can be rewound"), Plain->GetTimeable()->CanBeRewound());
			TestTrue(TEXT("Rewind"), Ability->TryRewindTarget(Plain->GetTimeable()));
			TestEqual(TEXT("Rewind charged"), Sand->GetCurrentSand(), 65.0f);
			TestFalse(TEXT("Can't rewind while rewinding"), Ability->TryRewindTarget(Plain->GetTimeable()));
			TestEqual(TEXT("Second rewind isn't charged"), Sand->GetCurrentSand(), 65.0f);

			TestTrue(TEXT("Rewinding"), Plain->GetTimeable()->IsRewinding());
			TestFalse(TEXT("Physics off while rewinding"), Plain->GetMesh()->IsSimulatingPhysics());
			TestNotNull(TEXT("Rewind overlay on"), Plain->GetMesh()->GetOverlayMaterial());

			Held->GetTimeable()->Rewind();
			Interrupted->GetTimeable()->Rewind();

			// the buffer is smaller than the fall so far
			TestEqual(TEXT("Ring buffer is capped"), ShortBuffer->GetTimeable()->GetBufferSeconds(), 0.5f);
			ShortBuffer->GetTimeable()->Rewind();

			State->PendulumClock = Pendulum->CaptureTimeState();
			TestTrue(TEXT("Pendulum can be rewound"), Pendulum->GetTimeable()->Rewind());
		}
		else if (Frame == 23)
		{
			const float Fallen = Start.Z - Plain->GetActorLocation().Z;
			TestTrue(TEXT("Rewinding moves it back up"), Fallen > 0.0f);
			TestTrue(TEXT("Pendulum clock runs backwards"), Pendulum->CaptureTimeState() < State->PendulumClock);

			// freezing mid-rewind stops it in place
			Interrupted->GetTimeable()->Freeze(1.0f);
			TestFalse(TEXT("Freeze stops the rewind"), Interrupted->GetTimeable()->IsRewinding());
			TestTrue(TEXT("Frozen mid-rewind"), Interrupted->GetTimeable()->IsFrozen());
			TestFalse(TEXT("Can't rewind while frozen"), Interrupted->GetTimeable()->CanBeRewound());
			State->FrozenLocation = Interrupted->GetActorLocation();
		}
		else if (Frame == 30)
		{
			TestEqual(TEXT("Stays where the freeze stopped it"), Interrupted->GetActorLocation(), State->FrozenLocation);
		}
		else if (Frame == 40)
		{
			// the rewinds took 20 frames at 1.5x, so about 14. Plain has had physics back for a few frames
			TestFalse(TEXT("Rewind ended"), Plain->GetTimeable()->IsRewinding());
			TestNull(TEXT("Overlay off"), Plain->GetMesh()->GetOverlayMaterial());
			TestEqual(TEXT("Dilation restored"), Plain->CustomTimeDilation, 1.0f);
			TestTrue(TEXT("Physics back"), Plain->GetMesh()->IsSimulatingPhysics());
			TestTrue(TEXT("Restarted with no velocity, so it's near the start"), Start.Z - Plain->GetActorLocation().Z < 100.0f);

			TestTrue(TEXT("Held"), Held->GetTimeable()->IsHolding());
			TestFalse(TEXT("Stays kinematic during the hold"), Held->GetMesh()->IsSimulatingPhysics());
			TestTrue(TEXT("Held at the start"), FMath::IsNearlyEqual(Held->GetActorLocation().Z, Start.Z, 5.0));
			TestFalse(TEXT("Used up its buffer"), Held->GetTimeable()->CanBeRewound());

			TestTrue(TEXT("Short buffer only got partway back"), Start.Z - ShortBuffer->GetActorLocation().Z > 100.0f);
			TestTrue(TEXT("Pendulum clock went back"), Pendulum->CaptureTimeState() < State->PendulumClock);
		}
		else if (Frame == 45)
		{
			// the freeze ended 2 frames ago. Interrupted resumes with the velocity it had, not none
			TestFalse(TEXT("Freeze over"), Interrupted->GetTimeable()->IsFrozen());
			TestTrue(TEXT("Physics back after the mid-rewind freeze"), Interrupted->GetMesh()->IsSimulatingPhysics());
			TestTrue(TEXT("Resumed with its recorded velocity"), Interrupted->GetMesh()->GetPhysicsLinearVelocity().Z < -100.0f);
		}
		else if (Frame == 60)
		{
			TestFalse(TEXT("Hold ended"), Held->GetTimeable()->IsHolding());
			TestTrue(TEXT("Physics back after the hold"), Held->GetMesh()->IsSimulatingPhysics());

			DestroyTestWorld(World);
			return true;
		}

		return false;
	}));

	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FRewindAuthoredTest, "Stopwatch.Rewind.AuthoredPast", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FRewindAuthoredTest::RunTest(const FString& Parameters)
{
	UWorld* World = MakeTestWorld();

	auto SpawnBridge = [World](float Hold)
	{
		ABrokenBridge* Bridge = World->SpawnActor<ABrokenBridge>();
		Bridge->GetTimeable()->HoldDuration = Hold;
		Bridge->GetTimeable()->RewindDuration = 0.5f;
		Bridge->GetTimeable()->CrumbleDuration = 0.5f;
		Bridge->DispatchBeginPlay();
		return Bridge;
	};

	ABrokenBridge* Bridge = SpawnBridge(1.0f);
	ABrokenBridge* Forever = SpawnBridge(0.0f);
	ABrokenBridge* Interrupted = SpawnBridge(1.0f);

	struct FState
	{
		int32 Frame = 0;
		FVector BrokenLocation = FVector::ZeroVector;
		float HoldLeft = 0.0f;
	};
	TSharedRef<FState> State = MakeShared<FState>();
	State->BrokenLocation = Bridge->GetPlanks()[0]->GetRelativeLocation();

	const FVector IntactLocation(Bridge->BridgeLength / Bridge->PlankCount * 0.5f, 0.0f, 0.0f);

	TestEqual(TEXT("One plank per PlankCount"), Bridge->GetPlanks().Num(), Bridge->PlankCount);
	TestTrue(TEXT("Starts fallen"), State->BrokenLocation.Z <= -Bridge->FallDepth);

	Bridge->GetTimeable()->Rewind();
	Forever->GetTimeable()->Rewind();
	Interrupted->GetTimeable()->Rewind();

	ADD_LATENT_AUTOMATION_COMMAND(FFunctionLatentCommand([=, this]()
	{
		World->Tick(LEVELTICK_All, TickSeconds);
		const int32 Frame = ++State->Frame;
		UTimeableComponent* Timeable = Bridge->GetTimeable();
		const FVector PlankLocation = Bridge->GetPlanks()[0]->GetRelativeLocation();

		if (Frame == 5)
		{
			TestTrue(TEXT("Rewinding"), Timeable->IsRewinding());
			TestTrue(TEXT("Partway there"), PlankLocation.Z > State->BrokenLocation.Z && PlankLocation.Z < 0.0f);

			Interrupted->GetTimeable()->Freeze(0.25f);
			TestFalse(TEXT("Freeze stops the rewind"), Interrupted->GetTimeable()->IsRewinding());
		}
		else if (Frame == 12)
		{
			TestTrue(TEXT("Holding after 0.5s"), Timeable->IsHolding());
			TestEqual(TEXT("Reached the intact pose"), PlankLocation, IntactLocation);
			TestFalse(TEXT("Already intact, nothing to rewind"), Timeable->CanBeRewound());

			// freezing a restored object pauses its countdown
			State->HoldLeft = Timeable->GetHoldTimeLeft();
			Timeable->Freeze(2.0f);
		}
		else if (Frame == 40)
		{
			TestTrue(TEXT("Freeze outlasts the rest of the hold, but it's still holding"), Timeable->IsHolding());
			TestEqual(TEXT("Countdown paused while frozen"), Timeable->GetHoldTimeLeft(), State->HoldLeft);
			TestEqual(TEXT("Still intact"), PlankLocation, IntactLocation);
		}
		else if (Frame == 70)
		{
			// the freeze ended around frame 52, then about 0.9s of hold is 18 frames
			TestFalse(TEXT("Countdown resumed and finished"), Timeable->IsHolding());
		}
		else if (Frame == 85)
		{
			TestEqual(TEXT("Crumbled back"), PlankLocation, State->BrokenLocation);
			TestTrue(TEXT("Can be rewound again"), Timeable->CanBeRewound());

			TestTrue(TEXT("HoldDuration 0 holds forever"), Forever->GetTimeable()->IsHolding());
			TestEqual(TEXT("A partly restored bridge crumbles after the freeze"), Interrupted->GetPlanks()[0]->GetRelativeLocation(), State->BrokenLocation);

			DestroyTestWorld(World);
			return true;
		}

		return false;
	}));

	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FLevelPieceRulesTest, "Stopwatch.LevelPieces.Rules", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FLevelPieceRulesTest::RunTest(const FString& Parameters)
{
	UWorld* World = MakeTestWorld();

	// ping-pong: covers the ends, the turnaround and a middle point of a path with three points
	AMovingPlatform* Platform = World->SpawnActor<AMovingPlatform>();
	Platform->Points = { FVector::ZeroVector, FVector(100.0f, 0.0f, 0.0f), FVector(100.0f, 100.0f, 0.0f) };
	Platform->SegmentTime = 1.0f;
	TestEqual(TEXT("Starts at the first point"), Platform->GetPathLocation(0.0f), Platform->Points[0]);
	TestEqual(TEXT("Eases through the middle of a segment"), Platform->GetPathLocation(0.5f), FVector(50.0f, 0.0f, 0.0f));
	TestEqual(TEXT("Reaches the middle point"), Platform->GetPathLocation(1.0f), Platform->Points[1]);
	TestEqual(TEXT("Reaches the last point"), Platform->GetPathLocation(2.0f), Platform->Points[2]);
	TestEqual(TEXT("Comes back through the middle point"), Platform->GetPathLocation(3.0f), Platform->Points[1]);
	TestEqual(TEXT("Loops"), Platform->GetPathLocation(4.0f), Platform->Points[0]);

	// rise, then hold, then slam, then rise again
	ACrusher* Crusher = World->SpawnActor<ACrusher>();
	Crusher->RiseTime = 1.0f;
	Crusher->HoldTime = 1.0f;
	Crusher->SlamTime = 1.0f;
	TestEqual(TEXT("Starts down"), Crusher->GetHeightFraction(0.0f), 0.0f);
	TestEqual(TEXT("Up during the hold"), Crusher->GetHeightFraction(1.5f), 1.0f);
	TestFalse(TEXT("Rising is safe"), Crusher->IsSlamming(0.5f));
	TestFalse(TEXT("Holding is safe"), Crusher->IsSlamming(1.5f));
	TestTrue(TEXT("Slamming"), Crusher->IsSlamming(2.5f));
	TestTrue(TEXT("Comes down during the slam"), Crusher->GetHeightFraction(2.5f) < 1.0f);
	TestFalse(TEXT("Next cycle rises again"), Crusher->IsSlamming(3.5f));

	// a chute won't remove a prop that's in use, but will remove others
	ADropChute* Chute = SpawnStarted<ADropChute>(World, FVector(0.0f, 0.0f, 10000.0f));
	Chute->Interval = 0.0f;
	Chute->MaxLiveProps = 1;
	Chute->ReleaseProp();
	TestEqual(TEXT("Released"), Chute->GetLiveProps().Num(), 1);
	APhysicsProp* First = Chute->GetLiveProps()[0].Get();
	First->GetTimeable()->Freeze(100.0f);
	Chute->ReleaseProp();
	TestEqual(TEXT("A frozen prop is kept"), Chute->GetLiveProps()[0].Get(), First);
	First->GetTimeable()->Unfreeze();
	Chute->ReleaseProp();
	TestNotEqual(TEXT("An unused prop is replaced"), Chute->GetLiveProps()[0].Get(), First);

	// the F1 debug overlay would otherwise also switch to wireframe
	for (const FKeyBind& Binding : GetDefault<UPlayerInput>()->DebugExecBindings)
	{
		TestFalse(TEXT("Engine's F1 viewmode binding is removed"), Binding.Key == EKeys::F1);
	}

	DestroyTestWorld(World);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FLevelPieceWorldTest, "Stopwatch.LevelPieces.World", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FLevelPieceWorldTest::RunTest(const FString& Parameters)
{
	UWorld* World = MakeTestWorld();

	AFallingPlatform* Platform = SpawnStarted<AFallingPlatform>(World, FVector(0.0f, 0.0f, 10000.0f));
	Platform->ShakeTime = 0.5f;
	const FVector Home = Platform->GetActorLocation();

	ACrusher* Crusher = SpawnStarted<ACrusher>(World, FVector(0.0f, 5000.0f, 0.0f));

	// the player: the abstract C++ class needs its blueprint
	UClass* CharacterClass = LoadClass<AStopwatchCharacter>(nullptr, TEXT("/Game/ThirdPerson/Blueprints/BP_ThirdPersonCharacter.BP_ThirdPersonCharacter_C"));
	AStopwatchCharacter* Character = nullptr;
	const FVector RespawnPoint(0.0f, -5000.0f, 500.0f);
	World->SpawnActor<APlayerStart>(RespawnPoint, FRotator::ZeroRotator);

	if (TestNotNull(TEXT("Character blueprint"), CharacterClass))
	{
		Character = World->SpawnActor<AStopwatchCharacter>(CharacterClass, FVector(0.0f, -7000.0f, 500.0f), FRotator::ZeroRotator);
		Character->DispatchBeginPlay();
		Character->GetTimeSand()->SetSand(5.0f);
	}

	struct FState
	{
		int32 Frame = 0;
		float CrusherClock = 0.0f;
		int32 Deaths = 0;
	};
	TSharedRef<FState> State = MakeShared<FState>();

	ADD_LATENT_AUTOMATION_COMMAND(FFunctionLatentCommand([=, this]()
	{
		World->Tick(LEVELTICK_All, TickSeconds);
		const int32 Frame = ++State->Frame;

		if (Frame == 2)
		{
			// after the first sample is recorded, as it would be for a platform that was waiting for the player
			Platform->StartShaking();
		}
		else if (Frame == 5)
		{
			TestFalse(TEXT("Still kinematic during the shake"), Platform->GetMesh()->IsSimulatingPhysics());
			TestTrue(TEXT("Shaking stays near home"), FVector::Dist(Platform->GetActorLocation(), Home) <= Platform->ShakeDistance + 0.1);

			// a stopped crusher's clock stops with it
			State->CrusherClock = Crusher->CaptureTimeState();
			Crusher->GetTimeable()->Freeze(0.25f);

			if (Character)
			{
				Character->KillPlayer(EDeathCause::Fall);
				Character->KillPlayer(EDeathCause::Blade);
				TestTrue(TEXT("Dying"), Character->IsDying());
				TestEqual(TEXT("Sand untouched until respawn"), Character->GetTimeSand()->GetCurrentSand(), 5.0f);
			}
		}
		else if (Frame == 8)
		{
			TestEqual(TEXT("Frozen crusher's clock is stopped"), Crusher->CaptureTimeState(), State->CrusherClock);
		}
		else if (Frame == 20)
		{
			TestTrue(TEXT("Dropping after the shake"), Platform->GetMesh()->IsSimulatingPhysics());
			TestTrue(TEXT("Has fallen"), Platform->GetActorLocation().Z < Home.Z);
			TestTrue(TEXT("Crusher resumed"), Crusher->CaptureTimeState() > State->CrusherClock);

			if (Character)
			{
				TestFalse(TEXT("Respawned"), Character->IsDying());
				TestEqual(TEXT("Respawn refills the sand"), Character->GetTimeSand()->GetCurrentSand(), Character->GetTimeSand()->MaxSand);
				TestTrue(TEXT("Respawned at the PlayerStart"), FVector::Dist2D(Character->GetActorLocation(), RespawnPoint) < 200.0);
			}

			Platform->GetTimeable()->Rewind();
		}
		else if (Frame == 50)
		{
			TestTrue(TEXT("Rewind brings it back and holds it"), Platform->GetTimeable()->IsHolding());
			TestFalse(TEXT("Stays kinematic"), Platform->GetMesh()->IsSimulatingPhysics());
			TestEqual(TEXT("Back at home"), Platform->CaptureTimeState(), 0.0f);
			TestTrue(TEXT("Near home"), FVector::Dist(Platform->GetActorLocation(), Home) <= Platform->ShakeDistance + 1.1);
		}
		else if (Frame == 55)
		{
			TestFalse(TEXT("Doesn't drop again during the hold"), Platform->GetMesh()->IsSimulatingPhysics());

			DestroyTestWorld(World);
			return true;
		}

		return false;
	}));

	return true;
}

#endif
