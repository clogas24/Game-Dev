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
}

void AFallingPlatform::StartShaking()
{
	bStepped = true;
}

void AFallingPlatform::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	// frozen, rewinding, already dropped, or held in place for the player to cross
	if (DeltaSeconds <= 0.0f || Mesh->IsSimulatingPhysics() || Timeable->IsHolding())
	{
		bStepped = false;
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
		SetActorLocation(HomeLocation);
		Mesh->SetSimulatePhysics(true);
		Mesh->WakeAllRigidBodies();
	}
	else
	{
		SetActorLocation(HomeLocation + FMath::VRand() * ShakeDistance);
	}
}
