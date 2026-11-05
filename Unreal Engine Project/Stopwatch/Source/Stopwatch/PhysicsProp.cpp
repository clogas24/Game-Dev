// Copyright Epic Games, Inc. All Rights Reserved.

#include "PhysicsProp.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "TimeableComponent.h"
#include "TimerManager.h"
#include "UObject/ConstructorHelpers.h"

APhysicsProp::APhysicsProp()
{
	// the engine's shapes are 100 units wide
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeFinder(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> SphereFinder(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	CubeMesh = CubeFinder.Object;
	SphereMesh = SphereFinder.Object;

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetStaticMesh(CubeMesh);
	Mesh->SetCollisionProfileName(TEXT("PhysicsActor"));
	Mesh->SetSimulatePhysics(true);
	Mesh->BodyInstance.SetMassOverride(Mass);
	RootComponent = Mesh;

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
}

void APhysicsProp::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);

	ApplySettings();
}

void APhysicsProp::ApplySettings()
{
	Mesh->SetStaticMesh(Shape == EPropShape::Sphere ? SphereMesh : CubeMesh);
	Mesh->SetMassOverrideInKg(NAME_None, Mass, true);
	Mesh->SetAngularDamping(AngularDamping);
	Mesh->SetLinearDamping(LinearDamping);
}

void APhysicsProp::BeginPlay()
{
	Super::BeginPlay();

	ApplySettings();

	SpawnTransform = GetActorTransform();

	if (bHeldUntilWoken)
	{
		Mesh->SetSimulatePhysics(false);
	}

	if (AutoResetInterval > 0.0f)
	{
		GetWorld()->GetTimerManager().SetTimer(AutoResetTimer, this, &APhysicsProp::AutoReset, AutoResetInterval, true);
	}
}

bool APhysicsProp::IsTimeLocked() const
{
	return Timeable->IsFrozen() || Timeable->IsRewinding() || Timeable->IsHolding();
}

void APhysicsProp::Wake(FVector Velocity)
{
	if (IsTimeLocked())
	{
		return;
	}

	Mesh->SetSimulatePhysics(true);
	Mesh->SetPhysicsLinearVelocity(Velocity);
	Mesh->WakeAllRigidBodies();
}

void APhysicsProp::AutoReset()
{
	if (IsTimeLocked())
	{
		return;
	}

	SetActorTransform(SpawnTransform, false, nullptr, ETeleportType::ResetPhysics);
	Mesh->SetPhysicsLinearVelocity(FVector::ZeroVector);
	Mesh->SetPhysicsAngularVelocityInDegrees(FVector::ZeroVector);
	Timeable->ClearHistory();
}

void APhysicsProp::ResetToStart()
{
	Timeable->ResetTimeState();
	SetActorTransform(SpawnTransform, false, nullptr, ETeleportType::ResetPhysics);
	Mesh->SetSimulatePhysics(!bHeldUntilWoken);

	if (!bHeldUntilWoken)
	{
		Mesh->SetPhysicsLinearVelocity(FVector::ZeroVector);
		Mesh->SetPhysicsAngularVelocityInDegrees(FVector::ZeroVector);
	}

	Timeable->ClearHistory();
}
