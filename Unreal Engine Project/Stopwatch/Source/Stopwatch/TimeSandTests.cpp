// Copyright Epic Games, Inc. All Rights Reserved.

#include "Components/StaticMeshComponent.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "FreezePendulum.h"
#include "GameFramework/Actor.h"
#include "Misc/AutomationTest.h"
#include "PhysicsCrate.h"
#include "TimeAbilityComponent.h"
#include "TimeableComponent.h"
#include "TimeSandComponent.h"
#include "Tests/AutomationCommon.h"

#if WITH_DEV_AUTOMATION_TESTS

// Run with: Automation RunTests Stopwatch.TimeSand
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTimeSandRulesTest, "Stopwatch.TimeSand.Rules", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FTimeSandRulesTest::RunTest(const FString& Parameters)
{
	UWorld* World = UWorld::CreateWorld(EWorldType::Game, false);
	FWorldContext& Context = GEngine->CreateNewWorldContext(EWorldType::Game);
	Context.SetCurrentWorld(World);

	AActor* Player = World->SpawnActor<AActor>();
	UTimeSandComponent* Sand = NewObject<UTimeSandComponent>(Player);
	UTimeAbilityComponent* Ability = NewObject<UTimeAbilityComponent>(Player);
	Player->AddInstanceComponent(Sand);
	Player->AddInstanceComponent(Ability);
	Sand->RegisterComponent();
	Ability->RegisterComponent();

	AActor* Crate = World->SpawnActor<AActor>();
	USceneComponent* CrateRoot = NewObject<USceneComponent>(Crate);
	Crate->SetRootComponent(CrateRoot);
	CrateRoot->RegisterComponent();
	UTimeableComponent* Timeable = NewObject<UTimeableComponent>(Crate);
	Crate->AddInstanceComponent(Timeable);
	Timeable->RegisterComponent();

	// Sand clamps and only spends what's affordable
	Sand->SetSand(500.0f);
	TestEqual(TEXT("SetSand clamps to max"), Sand->GetCurrentSand(), 100.0f);
	TestTrue(TEXT("Full"), Sand->IsFull());
	Sand->SetSand(-5.0f);
	TestEqual(TEXT("SetSand clamps to 0"), Sand->GetCurrentSand(), 0.0f);
	TestFalse(TEXT("Can't spend from empty"), Sand->TrySpend(1.0f));
	Sand->SetSand(90.0f);
	Sand->Add(35.0f);
	TestEqual(TEXT("Add clamps to max"), Sand->GetCurrentSand(), 100.0f);

	// Invalid targets cost nothing
	TestFalse(TEXT("No target"), Ability->TryFreezeTarget(nullptr));
	Timeable->bCanBeFrozen = false;
	TestFalse(TEXT("Immune target"), Ability->TryFreezeTarget(Timeable));
	TestFalse(TEXT("Immune target isn't frozen"), Timeable->IsFrozen());
	TestEqual(TEXT("Failed freezes are free"), Sand->GetCurrentSand(), 100.0f);
	Timeable->bCanBeFrozen = true;

	// Freezing charges, and refreezing charges the full amount again
	TestTrue(TEXT("Freeze"), Ability->TryFreezeTarget(Timeable));
	TestTrue(TEXT("Frozen"), Timeable->IsFrozen());
	TestEqual(TEXT("Crate time stopped"), Crate->CustomTimeDilation, 0.0f);
	TestEqual(TEXT("Freeze charged"), Sand->GetCurrentSand(), 75.0f);
	TestTrue(TEXT("Refreeze"), Ability->TryFreezeTarget(Timeable));
	TestEqual(TEXT("Refreeze charged"), Sand->GetCurrentSand(), 50.0f);

	Timeable->Unfreeze();
	TestFalse(TEXT("Unfrozen"), Timeable->IsFrozen());
	TestEqual(TEXT("Refreeze didn't lose the original dilation"), Crate->CustomTimeDilation, 1.0f);

	// Can't afford it: no freeze and no charge
	Sand->SetSand(24.0f);
	TestFalse(TEXT("Unaffordable"), Ability->TryFreezeTarget(Timeable));
	TestFalse(TEXT("Unaffordable isn't frozen"), Timeable->IsFrozen());
	TestEqual(TEXT("Unaffordable isn't charged"), Sand->GetCurrentSand(), 24.0f);

	// Exactly affordable works
	Sand->SetSand(25.0f);
	TestTrue(TEXT("Exactly affordable"), Ability->TryFreezeTarget(Timeable));
	TestEqual(TEXT("Spent to zero"), Sand->GetCurrentSand(), 0.0f);
	Timeable->Unfreeze();

	GEngine->DestroyWorldContext(World);
	World->DestroyWorld(false);
	return true;
}

// Ticks a real world with physics, one engine frame per world tick
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTimeableWorldTest, "Stopwatch.TimeSand.FreezeWorld", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FTimeableWorldTest::RunTest(const FString& Parameters)
{
	constexpr float TickSeconds = 0.05f;
	constexpr float FreezeSeconds = 1.0f;

	UWorld* World = UWorld::CreateWorld(EWorldType::Game, false);
	FWorldContext& Context = GEngine->CreateNewWorldContext(EWorldType::Game);
	Context.SetCurrentWorld(World);
	World->InitializeActorsForPlay(FURL());
	World->SetBegunPlay(true);

	// no floor, so the crate falls and has a velocity worth saving
	const FVector CrateStart(0.0f, 0.0f, 10000.0f);
	AFreezePendulum* Pendulum = World->SpawnActor<AFreezePendulum>();
	APhysicsCrate* Crate = World->SpawnActor<APhysicsCrate>(CrateStart, FRotator::ZeroRotator);
	Pendulum->DispatchBeginPlay();
	Crate->DispatchBeginPlay();

	struct FState
	{
		int32 Frame = 0;
		float FrozenAngle = 0.0f;
		FVector FrozenLocation = FVector::ZeroVector;
		float FrozenSpeedZ = 0.0f;
	};
	TSharedRef<FState> State = MakeShared<FState>();

	ADD_LATENT_AUTOMATION_COMMAND(FFunctionLatentCommand([this, World, Pendulum, Crate, State]()
	{
		World->Tick(LEVELTICK_All, TickSeconds);
		const int32 Frame = ++State->Frame;
		UStaticMeshComponent* Mesh = Crate->GetMesh();

		if (Frame == 20)
		{
			TestNotEqual(TEXT("Pendulum swings before freezing"), Pendulum->GetAngle(), 0.0f);
			TestTrue(TEXT("Crate falls before freezing"), Mesh->GetPhysicsLinearVelocity().Z < -100.0f);

			State->FrozenAngle = Pendulum->GetAngle();
			State->FrozenLocation = Crate->GetActorLocation();
			State->FrozenSpeedZ = Mesh->GetPhysicsLinearVelocity().Z;

			Pendulum->GetTimeable()->Freeze(FreezeSeconds);
			Crate->GetTimeable()->Freeze(FreezeSeconds);

			TestFalse(TEXT("Frozen crate isn't simulating"), Mesh->IsSimulatingPhysics());
			TestTrue(TEXT("Frozen crate keeps its collision"), Mesh->GetCollisionEnabled() == ECollisionEnabled::QueryAndPhysics);
			TestNotNull(TEXT("Overlay applied"), Mesh->GetOverlayMaterial());
		}
		else if (Frame == 30)
		{
			// half the freeze has passed
			TestTrue(TEXT("Still frozen"), Crate->GetTimeable()->IsFrozen());
			TestEqual(TEXT("Pendulum holds its angle"), Pendulum->GetAngle(), State->FrozenAngle);
			TestEqual(TEXT("Crate holds its position"), Crate->GetActorLocation(), State->FrozenLocation);

			Crate->AutoReset();
			TestEqual(TEXT("Auto reset skipped while frozen"), Crate->GetActorLocation(), State->FrozenLocation);
		}
		else if (Frame == 42)
		{
			// the dilation is still 0 on the owner, so only world time could have ended this
			TestFalse(TEXT("World time ended the freeze"), Crate->GetTimeable()->IsFrozen());
			TestTrue(TEXT("Crate simulates again"), Mesh->IsSimulatingPhysics());
			TestNull(TEXT("Overlay restored"), Mesh->GetOverlayMaterial());
			TestEqual(TEXT("Dilation restored"), Crate->CustomTimeDilation, 1.0f);
			TestTrue(TEXT("Crate kept its speed"), Mesh->GetPhysicsLinearVelocity().Z <= State->FrozenSpeedZ);
			TestNotEqual(TEXT("Pendulum resumes"), Pendulum->GetAngle(), State->FrozenAngle);

			const float AngleAfterResume = Pendulum->GetAngle();
			const float MaxStepDegrees = Pendulum->Amplitude * UE_TWO_PI / Pendulum->Period * TickSeconds * 3.0f;
			TestTrue(TEXT("Pendulum resumed from the same angle"), FMath::Abs(AngleAfterResume - State->FrozenAngle) < MaxStepDegrees);
		}
		else if (Frame == 45)
		{
			Crate->GetTimeable()->Freeze(FreezeSeconds);
			Crate->GetTimeable()->EndPlay(EEndPlayReason::Destroyed);
			TestFalse(TEXT("EndPlay unfreezes"), Crate->GetTimeable()->IsFrozen());
			TestNull(TEXT("EndPlay restores the overlay"), Mesh->GetOverlayMaterial());
			TestEqual(TEXT("EndPlay restores the dilation"), Crate->CustomTimeDilation, 1.0f);

			Crate->AutoReset();
			TestEqual(TEXT("Auto reset returns to spawn"), Crate->GetActorLocation(), FVector(0.0f, 0.0f, 10000.0f));
			TestEqual(TEXT("Auto reset zeroes velocity"), Mesh->GetPhysicsLinearVelocity(), FVector::ZeroVector);

			GEngine->DestroyWorldContext(World);
			World->DestroyWorld(false);
			return true;
		}

		return false;
	}));

	return true;
}

#endif
