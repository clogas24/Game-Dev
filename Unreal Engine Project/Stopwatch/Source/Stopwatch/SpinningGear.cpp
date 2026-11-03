// Copyright Epic Games, Inc. All Rights Reserved.

#include "SpinningGear.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "StopwatchCharacter.h"
#include "TimeableComponent.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	// how far the kill zone reaches past the arm, so the player dies before the arm's collision shoves them
	const float KillZoneMargin = 20.0f;
}

ASpinningGear::ASpinningGear()
{
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeFinder(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderFinder(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	CubeMesh = CubeFinder.Object;
	CylinderMesh = CylinderFinder.Object;

	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	Rotor = CreateDefaultSubobject<USceneComponent>(TEXT("Rotor"));
	Rotor->SetupAttachment(RootComponent);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Rotor);
	Mesh->SetCollisionProfileName(TEXT("BlockAll"));

	KillZone = CreateDefaultSubobject<UBoxComponent>(TEXT("KillZone"));
	KillZone->SetupAttachment(Rotor);
	KillZone->SetCollisionProfileName(TEXT("Trigger"));

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
}

void ASpinningGear::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);

	ApplySettings();
}

void ASpinningGear::BeginPlay()
{
	Super::BeginPlay();

	ApplySettings();
}

void ASpinningGear::ApplySettings()
{
	// the engine's shapes are 100 units wide
	const float Diameter = Radius * 2.0f / 100.0f;
	const float Height = Thickness / 100.0f;

	Mesh->SetStaticMesh(bIsBlade ? CubeMesh : CylinderMesh);
	Mesh->SetRelativeScale3D(bIsBlade ? FVector(Diameter, Height, Height) : FVector(Diameter, Diameter, Height));

	KillZone->SetBoxExtent(FVector(Radius, Thickness * 0.5f, Thickness * 0.5f) + FVector(KillZoneMargin));
	KillZone->SetCollisionEnabled(bIsBlade ? ECollisionEnabled::QueryOnly : ECollisionEnabled::NoCollision);

	UpdatePose();
}

void ASpinningGear::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	Elapsed += DeltaSeconds;
	UpdatePose();

	if (bIsBlade && DeltaSeconds > 0.0f)
	{
		AStopwatchCharacter::KillOverlappingPlayer(KillZone, EDeathCause::Blade);
	}
}

void ASpinningGear::RestoreTimeState(float State)
{
	Elapsed = State;
	UpdatePose();
}

void ASpinningGear::UpdatePose()
{
	Rotor->SetRelativeRotation(FQuat(Axis.GetSafeNormal(), FMath::DegreesToRadians(GetAngle())));
}
