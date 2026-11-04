// Copyright Epic Games, Inc. All Rights Reserved.

#include "ReturnPad.h"
#include "Components/BoxComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/Controller.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Stopwatch.h"
#include "StopwatchCharacter.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	const float PadRadius = 100.0f;
	const float PadHeight = 6.0f;
	const FLinearColor PadColor(0.1f, 1.0f, 0.4f);
}

AReturnPad::AReturnPad()
{
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));

	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	// the engine's cylinder is 100 units wide and tall, centered on its origin
	Pad = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Pad"));
	Pad->SetupAttachment(RootComponent);
	Pad->SetStaticMesh(CylinderMesh.Object);
	Pad->SetRelativeScale3D(FVector(PadRadius / 50.0f, PadRadius / 50.0f, PadHeight / 100.0f));
	Pad->SetRelativeLocation(FVector(0.0f, 0.0f, PadHeight * 0.5f));
	Pad->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	Box = CreateDefaultSubobject<UBoxComponent>(TEXT("Box"));
	Box->SetupAttachment(RootComponent);
	Box->SetCollisionProfileName(TEXT("Trigger"));
	Box->SetBoxExtent(FVector(PadRadius * 0.7f, PadRadius * 0.7f, 60.0f));
	Box->SetRelativeLocation(FVector(0.0f, 0.0f, 60.0f));
	Box->OnComponentBeginOverlap.AddDynamic(this, &AReturnPad::OnBoxBeginOverlap);
}

void AReturnPad::BeginPlay()
{
	Super::BeginPlay();

	if (UMaterialInstanceDynamic* Material = MakeClockworkMaterial(Pad))
	{
		Material->SetVectorParameterValue(TEXT("BaseColor"), PadColor);
		Material->SetVectorParameterValue(TEXT("RimColor"), PadColor);
		Material->SetScalarParameterValue(TEXT("RimAmount"), 2.0f);
	}
}

void AReturnPad::OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	AStopwatchCharacter* Character = Cast<AStopwatchCharacter>(OtherActor);

	if (!Character || Character->IsDying())
	{
		return;
	}

	const FRotator Facing(0.0f, DestinationYaw, 0.0f);
	Character->TeleportTo(Destination + FVector(0.0f, 0.0f, Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight()), Facing);
	Character->GetCharacterMovement()->StopMovementImmediately();

	if (AController* Controller = Character->GetController())
	{
		Controller->SetControlRotation(Facing);
	}
}
