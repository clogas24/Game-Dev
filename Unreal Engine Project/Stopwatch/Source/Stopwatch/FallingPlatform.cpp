// Copyright Epic Games, Inc. All Rights Reserved.

#include "FallingPlatform.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "StopwatchCharacter.h"
#include "TimeableComponent.h"
#include "UObject/ConstructorHelpers.h"

AFallingPlatform::AFallingPlatform()
{
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetStaticMesh(CubeMesh.Object);
	Mesh->SetCollisionProfileName(TEXT("PhysicsActor"));
	Mesh->SetSimulatePhysics(false);
	RootComponent = Mesh;

	// local units, so it stays on top however the actor is scaled
	StepTrigger = CreateDefaultSubobject<UBoxComponent>(TEXT("StepTrigger"));
	StepTrigger->SetupAttachment(Mesh);
	StepTrigger->SetCollisionProfileName(TEXT("Trigger"));
	StepTrigger->SetBoxExtent(FVector(50.0f, 50.0f, 25.0f));
	StepTrigger->SetRelativeLocation(FVector(0.0f, 0.0f, 50.0f));

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
	Timeable->bHoldAfterRewind = true;
}

void AFallingPlatform::BeginPlay()
{
	Super::BeginPlay();

	HomeLocation = GetActorLocation();
	HomeRotation = GetActorRotation();
}

void AFallingPlatform::StartShaking()
{
	bStepped = true;
}

void AFallingPlatform::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	// frozen or rewinding, so stays as it is
	if (DeltaSeconds <= 0.0f)
	{
		bStepped = false;
		return;
	}

	// already dropped, or held in place for the player to cross
	if (Mesh->IsSimulatingPhysics() || Timeable->IsHolding())
	{
		bStepped = false;
		Timeable->SetMaterialParameter(TEXT("Flash"), 0.0f);
		return;
	}

	if (ShakeElapsed <= 0.0f && !bStepped)
	{
		TArray<AActor*> Overlapping;
		StepTrigger->GetOverlappingActors(Overlapping, AStopwatchCharacter::StaticClass());

		if (Overlapping.IsEmpty())
		{
			return;
		}
	}

	bStepped = false;
	ShakeElapsed += DeltaSeconds;

	if (ShakeElapsed >= ShakeTime)
	{
		SetActorLocationAndRotation(HomeLocation, HomeRotation);
		Timeable->SetMaterialParameter(TEXT("Flash"), 0.0f);
		Mesh->SetSimulatePhysics(true);
		Mesh->WakeAllRigidBodies();
	}
	else
	{
		// tilts and bobs, but never moves sideways: pushed into a neighbor, physics would fling it out when the drop starts
		const float T = ShakeElapsed;
		const FRotator Wobble(WobbleDegrees * FMath::Sin(T * 23.0f), 0.0f, WobbleDegrees * 0.5f * FMath::Sin(T * 31.0f));
		SetActorLocationAndRotation(HomeLocation + FVector(0.0f, 0.0f, ShakeDistance * FMath::Sin(T * 47.0f)), HomeRotation + Wobble);
		Timeable->SetMaterialParameter(TEXT("Flash"), 0.5f + 0.5f * FMath::Sin(T * 25.0f));
	}
}

void AFallingPlatform::ResetToStart()
{
	Timeable->ResetTimeState();
	Mesh->SetSimulatePhysics(false);
	SetActorLocationAndRotation(HomeLocation, HomeRotation, false, nullptr, ETeleportType::ResetPhysics);
	Timeable->SetMaterialParameter(TEXT("Flash"), 0.0f);
	ShakeElapsed = 0.0f;
	bStepped = false;
}
