// Copyright Epic Games, Inc. All Rights Reserved.

#include "PhysicsCrate.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "TimeableComponent.h"
#include "TimerManager.h"
#include "UObject/ConstructorHelpers.h"

APhysicsCrate::APhysicsCrate()
{
	// the engine's cube is 100 units wide
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetStaticMesh(CubeMesh.Object);
	Mesh->SetCollisionProfileName(TEXT("PhysicsActor"));
	Mesh->SetSimulatePhysics(true);

	// twice the default character's 100 kg, so standing on it doesn't shove it around
	Mesh->BodyInstance.SetMassOverride(200.0f);
	RootComponent = Mesh;

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
}

void APhysicsCrate::BeginPlay()
{
	Super::BeginPlay();

	SpawnTransform = GetActorTransform();

	if (AutoResetInterval > 0.0f)
	{
		GetWorld()->GetTimerManager().SetTimer(AutoResetTimer, this, &APhysicsCrate::AutoReset, AutoResetInterval, true);
	}
}

void APhysicsCrate::AutoReset()
{
	if (Timeable->IsFrozen())
	{
		return;
	}

	SetActorTransform(SpawnTransform, false, nullptr, ETeleportType::ResetPhysics);
	Mesh->SetPhysicsLinearVelocity(FVector::ZeroVector);
	Mesh->SetPhysicsAngularVelocityInDegrees(FVector::ZeroVector);
}
