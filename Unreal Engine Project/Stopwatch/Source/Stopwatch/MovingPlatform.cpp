// Copyright Epic Games, Inc. All Rights Reserved.

#include "MovingPlatform.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "TimeableComponent.h"
#include "UObject/ConstructorHelpers.h"

AMovingPlatform::AMovingPlatform()
{
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));

	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(RootComponent);
	Mesh->SetStaticMesh(CubeMesh.Object);
	Mesh->SetCollisionProfileName(TEXT("BlockAll"));

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
}

void AMovingPlatform::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);

	ApplySettings();
}

void AMovingPlatform::BeginPlay()
{
	Super::BeginPlay();

	ApplySettings();
}

void AMovingPlatform::ApplySettings()
{
	// the engine's cube is 100 units wide
	Mesh->SetRelativeScale3D(Size / 100.0f);

	UpdatePose();
}

void AMovingPlatform::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	Elapsed += DeltaSeconds;
	UpdatePose();
}

void AMovingPlatform::RestoreTimeState(float State)
{
	Elapsed = State;
	UpdatePose();
}

FVector AMovingPlatform::GetPathLocation(float Time) const
{
	if (Points.Num() < 2)
	{
		return Points.IsEmpty() ? FVector::ZeroVector : Points[0];
	}

	// out along the path then back: Progress runs over the segments, and past the end it mirrors
	const int32 Segments = Points.Num() - 1;
	float Progress = FMath::Fmod(Time / SegmentTime, Segments * 2.0f);

	if (Progress > Segments)
	{
		Progress = Segments * 2.0f - Progress;
	}

	const int32 Segment = FMath::Min(FMath::FloorToInt(Progress), Segments - 1);
	return FMath::Lerp(Points[Segment], Points[Segment + 1], FMath::SmoothStep(0.0f, 1.0f, Progress - Segment));
}

void AMovingPlatform::UpdatePose()
{
	Mesh->SetRelativeLocation(GetPathLocation(Elapsed));
}

void AMovingPlatform::ResetToStart()
{
	Timeable->ResetTimeState();
	RestoreTimeState(0.0f);
}
