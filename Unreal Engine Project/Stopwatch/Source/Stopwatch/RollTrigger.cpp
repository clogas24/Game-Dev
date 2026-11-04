// Copyright Epic Games, Inc. All Rights Reserved.

#include "RollTrigger.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "DropChute.h"
#include "Engine/StaticMesh.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "PhysicsProp.h"
#include "Stopwatch.h"
#include "StopwatchCharacter.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	const float PlateRadius = 120.0f;
	const float PlateHeight = 12.0f;
	const float PressDepth = 8.0f;
	const float PressSpeed = 12.0f;
}

ARollTrigger::ARollTrigger()
{
	PrimaryActorTick.bCanEverTick = true;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));

	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	// the engine's cylinder is 100 units wide and tall, centered on its origin
	Plate = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Plate"));
	Plate->SetupAttachment(RootComponent);
	Plate->SetStaticMesh(CylinderMesh.Object);
	Plate->SetRelativeScale3D(FVector(PlateRadius / 50.0f, PlateRadius / 50.0f, PlateHeight / 100.0f));
	Plate->SetRelativeLocation(FVector(0.0f, 0.0f, PlateHeight * 0.5f));
	Plate->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	Box = CreateDefaultSubobject<UBoxComponent>(TEXT("Box"));
	Box->SetupAttachment(RootComponent);
	Box->SetCollisionProfileName(TEXT("Trigger"));
	Box->SetBoxExtent(FVector(PlateRadius, PlateRadius, 60.0f));
	Box->SetRelativeLocation(FVector(0.0f, 0.0f, 60.0f));
	Box->OnComponentBeginOverlap.AddDynamic(this, &ARollTrigger::OnBoxBeginOverlap);
	Box->OnComponentEndOverlap.AddDynamic(this, &ARollTrigger::OnBoxEndOverlap);
}

void ARollTrigger::BeginPlay()
{
	Super::BeginPlay();

	PlateMaterial = MakeClockworkMaterial(Plate);
	UpdateColor();
}

void ARollTrigger::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	const float Target = PlateHeight * 0.5f - (IsPressed() ? PressDepth : 0.0f);
	const FVector Location = Plate->GetRelativeLocation();
	Plate->SetRelativeLocation(FVector(Location.X, Location.Y, FMath::FInterpTo(Location.Z, Target, DeltaSeconds, PressSpeed)));
}

void ARollTrigger::UpdateColor()
{
	if (!PlateMaterial)
	{
		return;
	}

	const FLinearColor Color = IsPressed() ? FLinearColor(0.1f, 0.8f, 0.2f) : FLinearColor(0.9f, 0.1f, 0.05f);
	PlateMaterial->SetVectorParameterValue(TEXT("BaseColor"), Color);
	PlateMaterial->SetVectorParameterValue(TEXT("RimColor"), Color);
	PlateMaterial->SetScalarParameterValue(TEXT("RimAmount"), 0.6f);
}

void ARollTrigger::OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	if (!Cast<AStopwatchCharacter>(OtherActor))
	{
		return;
	}

	++PressCount;
	UpdateColor();

	if (TargetProp)
	{
		TargetProp->Wake(PushVelocity);
	}

	if (TargetChute)
	{
		TargetChute->ReleaseProp();
	}
}

void ARollTrigger::OnBoxEndOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex)
{
	if (Cast<AStopwatchCharacter>(OtherActor))
	{
		PressCount = FMath::Max(PressCount - 1, 0);
		UpdateColor();
	}
}
