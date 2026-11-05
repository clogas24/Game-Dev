// Copyright Epic Games, Inc. All Rights Reserved.

#include "DropChute.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "TimeableComponent.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	// clears the chute's own mesh, whose bottom is 50 units below the actor
	const float ReleaseDrop = 150.0f;
}

ADropChute::ADropChute()
{
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetStaticMesh(CubeMesh.Object);
	Mesh->SetCollisionProfileName(TEXT("BlockAll"));
	RootComponent = Mesh;

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
}

void ADropChute::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	if (Interval <= 0.0f)
	{
		return;
	}

	Elapsed += DeltaSeconds;

	if (Elapsed >= Interval)
	{
		Elapsed = 0.0f;
		ReleaseProp();
	}
}

void ADropChute::ReleaseProp()
{
	LiveProps.RemoveAll([](const TWeakObjectPtr<APhysicsProp>& Prop) { return !Prop.IsValid(); });

	if (LiveProps.Num() >= MaxLiveProps)
	{
		const int32 Removable = LiveProps.IndexOfByPredicate([](const TWeakObjectPtr<APhysicsProp>& Prop)
		{
			const UTimeableComponent* PropTimeable = Prop->GetTimeable();
			return !PropTimeable->IsFrozen() && !PropTimeable->IsRewinding() && !PropTimeable->IsHolding();
		});

		if (Removable == INDEX_NONE)
		{
			return;
		}

		LiveProps[Removable]->Destroy();
		LiveProps.RemoveAt(Removable);
	}

	const FTransform SpawnTransform(GetActorRotation(), GetActorLocation() - FVector(0.0f, 0.0f, ReleaseDrop), PropScale);
	APhysicsProp* Prop = GetWorld()->SpawnActorDeferred<APhysicsProp>(APhysicsProp::StaticClass(), SpawnTransform, this, nullptr, ESpawnActorCollisionHandlingMethod::AlwaysSpawn);
	Prop->Shape = PropShape;
	Prop->Mass = PropMass;
	Prop->LinearDamping = PropLinearDamping;
	Prop->FinishSpawning(SpawnTransform);

	LiveProps.Add(Prop);
}

void ADropChute::ResetToStart()
{
	Timeable->ResetTimeState();

	for (const TWeakObjectPtr<APhysicsProp>& Prop : LiveProps)
	{
		if (Prop.IsValid())
		{
			Prop->Destroy();
		}
	}

	LiveProps.Reset();
	Elapsed = 0.0f;
}
