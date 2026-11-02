// Copyright Epic Games, Inc. All Rights Reserved.

#include "FreezePendulum.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "TimeableComponent.h"
#include "UObject/ConstructorHelpers.h"

AFreezePendulum::AFreezePendulum()
{
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> SphereMesh(TEXT("/Engine/BasicShapes/Sphere.Sphere"));

	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	Pivot = CreateDefaultSubobject<USceneComponent>(TEXT("Pivot"));
	Pivot->SetupAttachment(RootComponent);

	Arm = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Arm"));
	Arm->SetupAttachment(Pivot);
	Arm->SetStaticMesh(CylinderMesh.Object);
	Arm->SetCollisionProfileName(TEXT("BlockAll"));

	// the bob ignores the arm's stretched scale. The cylinder's end is always 50 units from its center in local space
	Bob = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Bob"));
	Bob->SetupAttachment(Arm);
	Bob->SetStaticMesh(SphereMesh.Object);
	Bob->SetUsingAbsoluteScale(true);
	Bob->SetRelativeScale3D(FVector(0.6f));
	Bob->SetRelativeLocation(FVector(0.0f, 0.0f, -50.0f));
	Bob->SetCollisionProfileName(TEXT("BlockAll"));

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
}

void AFreezePendulum::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);

	// the engine's cylinder is 100 units tall and centered on its origin
	Arm->SetRelativeScale3D(FVector(0.1f, 0.1f, ArmLength / 100.0f));
	Arm->SetRelativeLocation(FVector(0.0f, 0.0f, -ArmLength * 0.5f));

	UpdateSwing();
}

void AFreezePendulum::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	Elapsed += DeltaSeconds;
	UpdateSwing();
}

float AFreezePendulum::GetAngle() const
{
	return Amplitude * FMath::Sin(UE_TWO_PI * Elapsed / Period + PhaseOffset);
}

void AFreezePendulum::UpdateSwing()
{
	Pivot->SetRelativeRotation(FQuat(SwingAxis.GetSafeNormal(), FMath::DegreesToRadians(GetAngle())));
}
