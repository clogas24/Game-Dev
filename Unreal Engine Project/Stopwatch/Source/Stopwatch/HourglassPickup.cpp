// Copyright Epic Games, Inc. All Rights Reserved.

#include "HourglassPickup.h"
#include "PlaytestLogger.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Stopwatch.h"
#include "TimerManager.h"
#include "TimeSandComponent.h"
#include "UObject/ConstructorHelpers.h"

AHourglassPickup::AHourglassPickup()
{
	PrimaryActorTick.bCanEverTick = true;

	Trigger = CreateDefaultSubobject<USphereComponent>(TEXT("Trigger"));
	Trigger->InitSphereRadius(100.0f);
	Trigger->SetCollisionProfileName(TEXT("Trigger"));
	Trigger->OnComponentBeginOverlap.AddDynamic(this, &AHourglassPickup::OnTriggerBeginOverlap);
	Trigger->OnComponentEndOverlap.AddDynamic(this, &AHourglassPickup::OnTriggerEndOverlap);
	RootComponent = Trigger;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(RootComponent);
	Mesh->SetStaticMesh(CylinderMesh.Object);
	Mesh->SetRelativeScale3D(FVector(0.3f, 0.3f, 0.6f));
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void AHourglassPickup::BeginPlay()
{
	Super::BeginPlay();

	// the sand bar's color, glowing so it stands out on the floor
	if (UMaterialInstanceDynamic* Material = MakeClockworkMaterial(Mesh))
	{
		const FLinearColor SandColor(0.9f, 0.65f, 0.2f);
		Material->SetVectorParameterValue(TEXT("BaseColor"), SandColor);
		Material->SetVectorParameterValue(TEXT("RimColor"), SandColor);
		Material->SetScalarParameterValue(TEXT("RimAmount"), 1.5f);
	}
}

void AHourglassPickup::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);

	BobTime += DeltaTime;
	Mesh->SetRelativeLocation(FVector(0.0f, 0.0f, BobHeight * FMath::Sin(BobTime * 2.0f)));
	Mesh->AddRelativeRotation(FRotator(0.0f, SpinSpeed * DeltaTime, 0.0f));

	if (UTimeSandComponent* Sand = WaitingSand.Get())
	{
		if (!Sand->IsFull())
		{
			Collect(Sand);
		}
	}
}

void AHourglassPickup::OnTriggerBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	UTimeSandComponent* Sand = OtherActor ? OtherActor->FindComponentByClass<UTimeSandComponent>() : nullptr;

	if (!Sand)
	{
		return;
	}

	if (Sand->IsFull())
	{
		WaitingSand = Sand;
	}
	else
	{
		Collect(Sand);
	}
}

void AHourglassPickup::OnTriggerEndOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex)
{
	if (OtherActor && WaitingSand.IsValid() && WaitingSand->GetOwner() == OtherActor)
	{
		WaitingSand = nullptr;
	}
}

void AHourglassPickup::Collect(UTimeSandComponent* Sand)
{
	const float Before = Sand->GetCurrentSand();
	Sand->Add(SandAmount);
	WaitingSand = nullptr;

	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->RecordSandPickedUp(Sand->GetCurrentSand() - Before);
	}

	// hidden rather than destroyed, so a checkpoint reset can bring back even a one-off pickup
	SetActorHiddenInGame(true);
	SetActorEnableCollision(false);
	SetActorTickEnabled(false);

	if (RespawnTime > 0.0f)
	{
		GetWorld()->GetTimerManager().SetTimer(RespawnTimer, this, &AHourglassPickup::Respawn, RespawnTime);
	}
}

void AHourglassPickup::Respawn()
{
	SetActorHiddenInGame(false);
	SetActorEnableCollision(true);
	SetActorTickEnabled(true);
}

void AHourglassPickup::ResetToStart()
{
	GetWorld()->GetTimerManager().ClearTimer(RespawnTimer);
	WaitingSand = nullptr;
	Respawn();
}
