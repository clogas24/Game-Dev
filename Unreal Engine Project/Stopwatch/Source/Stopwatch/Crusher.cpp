// Copyright Epic Games, Inc. All Rights Reserved.

#include "Crusher.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "StopwatchCharacter.h"
#include "TimeableComponent.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	// how far the kill zone reaches below the head, so the player dies before the head's collision shoves them
	const float KillZoneReach = 30.0f;
}

ACrusher::ACrusher()
{
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));

	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	Head = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Head"));
	Head->SetupAttachment(RootComponent);
	Head->SetStaticMesh(CubeMesh.Object);
	Head->SetCollisionProfileName(TEXT("BlockAll"));

	KillZone = CreateDefaultSubobject<UBoxComponent>(TEXT("KillZone"));
	KillZone->SetupAttachment(RootComponent);
	KillZone->SetCollisionProfileName(TEXT("Trigger"));

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
}

void ACrusher::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);

	ApplySettings();
}

void ACrusher::BeginPlay()
{
	Super::BeginPlay();

	ApplySettings();
}

void ACrusher::ApplySettings()
{
	// the engine's cube is 100 units wide
	Head->SetRelativeScale3D(HeadSize / 100.0f);
	KillZone->SetBoxExtent(FVector(HeadSize.X * 0.5f, HeadSize.Y * 0.5f, KillZoneReach));

	UpdatePose();
}

void ACrusher::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	Elapsed += DeltaSeconds;
	UpdatePose();

	if (DeltaSeconds > 0.0f && IsSlamming(Elapsed))
	{
		AStopwatchCharacter::KillOverlappingPlayer(KillZone, EDeathCause::Crushed);
	}
}

void ACrusher::RestoreTimeState(float State)
{
	Elapsed = State;
	UpdatePose();
}

float ACrusher::GetHeightFraction(float Time) const
{
	const float Cycle = FMath::Fmod(Time + PhaseOffset, RiseTime + HoldTime + SlamTime);

	if (Cycle < RiseTime)
	{
		return FMath::SmoothStep(0.0f, 1.0f, Cycle / RiseTime);
	}

	if (Cycle < RiseTime + HoldTime)
	{
		return 1.0f;
	}

	// accelerates into the floor
	const float SlamFraction = (Cycle - RiseTime - HoldTime) / SlamTime;
	return 1.0f - SlamFraction * SlamFraction;
}

bool ACrusher::IsSlamming(float Time) const
{
	return FMath::Fmod(Time + PhaseOffset, RiseTime + HoldTime + SlamTime) >= RiseTime + HoldTime;
}

void ACrusher::UpdatePose()
{
	const float Bottom = TravelHeight * GetHeightFraction(Elapsed);
	Head->SetRelativeLocation(FVector(0.0f, 0.0f, Bottom + HeadSize.Z * 0.5f));
	KillZone->SetRelativeLocation(FVector(0.0f, 0.0f, Bottom));
}
