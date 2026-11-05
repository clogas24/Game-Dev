// Copyright Epic Games, Inc. All Rights Reserved.

#include "LevelTriggers.h"
#include "Components/BoxComponent.h"
#include "Components/PointLightComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "PlaytestLogger.h"
#include "Stopwatch.h"
#include "StopwatchCharacter.h"
#include "StopwatchGameMode.h"
#include "StopwatchHUDWidget.h"
#include "StopwatchPlayerController.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	const FLinearColor CheckpointUnlit(0.05f, 0.25f, 0.08f);
	const FLinearColor CheckpointLit(0.1f, 1.0f, 0.3f);
	constexpr float PedestalRadius = 120.0f;
	constexpr float PedestalHeight = 30.0f;
}

APlayerTrigger::APlayerTrigger()
{
	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	Box = CreateDefaultSubobject<UBoxComponent>(TEXT("Box"));
	Box->SetupAttachment(RootComponent);
	Box->SetCollisionProfileName(TEXT("Trigger"));
	Box->SetBoxExtent(FVector(150.0f, 150.0f, 150.0f));
	Box->SetRelativeLocation(FVector(0.0f, 0.0f, 150.0f));
	Box->OnComponentBeginOverlap.AddDynamic(this, &APlayerTrigger::OnBoxBeginOverlap);
}

void APlayerTrigger::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);

	Box->SetRelativeLocation(FVector(0.0f, 0.0f, Box->GetUnscaledBoxExtent().Z));
}

void APlayerTrigger::BeginPlay()
{
	Box->SetRelativeLocation(FVector(0.0f, 0.0f, Box->GetUnscaledBoxExtent().Z));

	Super::BeginPlay();
}

AStopwatchGameMode* APlayerTrigger::GetStopwatchGameMode() const
{
	return GetWorld()->GetAuthGameMode<AStopwatchGameMode>();
}

void APlayerTrigger::OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	AStopwatchCharacter* Character = Cast<AStopwatchCharacter>(OtherActor);

	if (Character && !Character->IsDying())
	{
		OnPlayerEnter(Character);
	}
}

ACheckpoint::ACheckpoint()
{
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));

	// the actor's location is where the player's feet arrive
	GetBox()->SetBoxExtent(FVector(PedestalRadius, PedestalRadius, 120.0f));
	GetBox()->SetRelativeLocation(FVector(0.0f, 0.0f, 120.0f));

	// the engine's cylinder is 100 units wide and tall, centered on its origin
	Pedestal = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Pedestal"));
	Pedestal->SetupAttachment(RootComponent);
	Pedestal->SetStaticMesh(CylinderMesh.Object);
	Pedestal->SetRelativeScale3D(FVector(PedestalRadius / 50.0f, PedestalRadius / 50.0f, PedestalHeight / 100.0f));
	Pedestal->SetRelativeLocation(FVector(0.0f, 0.0f, PedestalHeight * 0.5f));
	Pedestal->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	Light = CreateDefaultSubobject<UPointLightComponent>(TEXT("Light"));
	Light->SetupAttachment(RootComponent);
	Light->SetRelativeLocation(FVector(0.0f, 0.0f, 150.0f));
	Light->SetLightColor(CheckpointLit);
	Light->SetIntensity(0.0f);
	Light->SetAttenuationRadius(600.0f);
	Light->SetCastShadows(false);
}

void ACheckpoint::BeginPlay()
{
	Super::BeginPlay();

	PedestalMaterial = MakeClockworkMaterial(Pedestal);
	SetLit(bIsLit);
}

bool ACheckpoint::IsAfter(const ACheckpoint* Other) const
{
	return !Other || SectionId > Other->SectionId || (SectionId == Other->SectionId && OrderInSection > Other->OrderInSection);
}

void ACheckpoint::SetLit(bool bLit)
{
	bIsLit = bLit;
	Light->SetIntensity(bLit ? LitIntensity : 0.0f);

	if (PedestalMaterial)
	{
		const FLinearColor Color = bLit ? CheckpointLit : CheckpointUnlit;
		PedestalMaterial->SetVectorParameterValue(TEXT("BaseColor"), Color);
		PedestalMaterial->SetVectorParameterValue(TEXT("RimColor"), Color);
		PedestalMaterial->SetScalarParameterValue(TEXT("RimAmount"), bLit ? 2.0f : 0.3f);
	}
}

void ACheckpoint::OnPlayerEnter(AStopwatchCharacter* Character)
{
	if (AStopwatchGameMode* GameMode = GetStopwatchGameMode())
	{
		GameMode->ReachCheckpoint(this);
	}
}

void ASectionTrigger::OnPlayerEnter(AStopwatchCharacter* Character)
{
	if (AStopwatchGameMode* GameMode = GetStopwatchGameMode())
	{
		GameMode->EnterSection(SectionId, DisplayName);
	}
}

void ATutorialTrigger::OnPlayerEnter(AStopwatchCharacter* Character)
{
	const AStopwatchGameMode* GameMode = GetStopwatchGameMode();
	AStopwatchPlayerController* Controller = Character->GetController<AStopwatchPlayerController>();

	if (!GameMode || !GameMode->IsInPlay() || !Controller || !Controller->GetHUDWidget())
	{
		return;
	}

	// once per session, so a restart or a respawn doesn't repeat it
	UPlaytestLogger* Logger = UPlaytestLogger::Get(this);

	if (Logger && !Logger->MarkTutorialShown(GetFName()))
	{
		return;
	}

	Controller->GetHUDWidget()->ShowTutorial(Prompt, DisplaySeconds);
}

void AGoalTrigger::OnPlayerEnter(AStopwatchCharacter* Character)
{
	if (AStopwatchGameMode* GameMode = GetStopwatchGameMode())
	{
		GameMode->Win();
	}
}

void ACollapseTrigger::OnPlayerEnter(AStopwatchCharacter* Character)
{
	if (AStopwatchGameMode* GameMode = GetStopwatchGameMode())
	{
		GameMode->StartCollapse(this);
	}
}
