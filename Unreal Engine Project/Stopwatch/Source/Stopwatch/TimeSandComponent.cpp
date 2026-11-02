// Copyright Epic Games, Inc. All Rights Reserved.

#include "TimeSandComponent.h"
#include "Engine/World.h"
#include "Stopwatch.h"

UTimeSandComponent::UTimeSandComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
}

void UTimeSandComponent::BeginPlay()
{
	Super::BeginPlay();

	CurrentSand = MaxSand;
	OnSandChanged.Broadcast(CurrentSand, MaxSand);
}

void UTimeSandComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);

	if (RegenPerSecond > 0.0f)
	{
		// DeltaTime is dilated, regen runs on real time
		Add(RegenPerSecond * GetWorld()->DeltaRealTimeSeconds);
	}
}

bool UTimeSandComponent::TrySpend(float Amount)
{
	if (!CanAfford(Amount))
	{
		UE_LOG(LogStopwatch, Log, TEXT("Can't afford %.0f sand (have %.0f)"), Amount, CurrentSand);
		OnSpendFailed.Broadcast();
		return false;
	}

	SetSand(CurrentSand - Amount);
	return true;
}

void UTimeSandComponent::Add(float Amount)
{
	SetSand(CurrentSand + Amount);
}

void UTimeSandComponent::SetSand(float NewSand)
{
	const float Clamped = FMath::Clamp(NewSand, 0.0f, MaxSand);

	if (Clamped != CurrentSand)
	{
		CurrentSand = Clamped;
		OnSandChanged.Broadcast(CurrentSand, MaxSand);
	}
}
