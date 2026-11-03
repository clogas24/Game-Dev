// Copyright Epic Games, Inc. All Rights Reserved.

#include "TimeAbilityComponent.h"
#include "Engine/World.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/PlayerController.h"
#include "Stopwatch.h"
#include "TimeableComponent.h"
#include "TimeSandComponent.h"

UTimeAbilityComponent::UTimeAbilityComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
}

void UTimeAbilityComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);

	const UTimeSandComponent* Sand = GetOwner()->FindComponentByClass<UTimeSandComponent>();

	UTimeableComponent* Target = FindAimTarget();
	AimTarget = Target;

	ETimeAimState NewState = ETimeAimState::NoTarget;
	if (Sand && Target)
	{
		if (Target->CanBeFrozen() && Sand->CanAfford(FreezeSandCost))
		{
			NewState = ETimeAimState::CanFreeze;
		}
		else if (Target->CanBeRewound() && Sand->CanAfford(RewindSandCost))
		{
			NewState = ETimeAimState::RewindOnly;
		}
		else
		{
			NewState = ETimeAimState::Unaffordable;
		}
	}

	if (NewState != AimState)
	{
		AimState = NewState;
		OnAimStateChanged.Broadcast(AimState);
	}
}

UTimeableComponent* UTimeAbilityComponent::FindAimTarget() const
{
	const APawn* Pawn = Cast<APawn>(GetOwner());
	const APlayerController* PlayerController = Pawn ? Pawn->GetController<APlayerController>() : nullptr;

	if (!PlayerController)
	{
		return nullptr;
	}

	FVector ViewLocation;
	FRotator ViewRotation;
	PlayerController->GetPlayerViewPoint(ViewLocation, ViewRotation);

	FCollisionQueryParams Params(SCENE_QUERY_STAT(FreezeAim), false, Pawn);
	FHitResult Hit;

	if (!GetWorld()->LineTraceSingleByChannel(Hit, ViewLocation, ViewLocation + ViewRotation.Vector() * FreezeRange, ECC_Visibility, Params))
	{
		return nullptr;
	}

	UTimeableComponent* Target = Hit.GetActor() ? Hit.GetActor()->FindComponentByClass<UTimeableComponent>() : nullptr;
	return Target && (Target->CanBeFrozen() || Target->CanBeRewound()) ? Target : nullptr;
}

bool UTimeAbilityComponent::TryFreeze()
{
	return TryFreezeTarget(FindAimTarget());
}

bool UTimeAbilityComponent::TryFreezeTarget(UTimeableComponent* Target)
{
	if (!Target || !Target->CanBeFrozen())
	{
		return false;
	}

	UTimeSandComponent* Sand = GetOwner()->FindComponentByClass<UTimeSandComponent>();

	if (!Sand)
	{
		UE_LOG(LogStopwatch, Error, TEXT("'%s' has no UTimeSandComponent to pay for Freeze."), *GetNameSafe(GetOwner()));
		return false;
	}

	if (!Sand->TrySpend(FreezeSandCost))
	{
		UE_LOG(LogStopwatch, Log, TEXT("Freeze failed: not enough sand for '%s'."), *GetNameSafe(Target->GetOwner()));
		return false;
	}

	Target->Freeze(FreezeDuration);
	OnAbilityUsed.Broadcast();
	UE_LOG(LogStopwatch, Log, TEXT("Froze '%s' for %.1fs, %.0f sand left."), *GetNameSafe(Target->GetOwner()), FreezeDuration, Sand->GetCurrentSand());
	return true;
}

bool UTimeAbilityComponent::TryRewind()
{
	return TryRewindTarget(FindAimTarget());
}

bool UTimeAbilityComponent::TryRewindTarget(UTimeableComponent* Target)
{
	if (!Target || !Target->CanBeRewound())
	{
		return false;
	}

	UTimeSandComponent* Sand = GetOwner()->FindComponentByClass<UTimeSandComponent>();

	if (!Sand)
	{
		UE_LOG(LogStopwatch, Error, TEXT("'%s' has no UTimeSandComponent to pay for Rewind."), *GetNameSafe(GetOwner()));
		return false;
	}

	if (!Sand->TrySpend(RewindSandCost))
	{
		UE_LOG(LogStopwatch, Log, TEXT("Rewind failed: not enough sand for '%s'."), *GetNameSafe(Target->GetOwner()));
		return false;
	}

	Target->Rewind();
	OnAbilityUsed.Broadcast();
	UE_LOG(LogStopwatch, Log, TEXT("Rewound '%s', %.0f sand left."), *GetNameSafe(Target->GetOwner()), Sand->GetCurrentSand());
	return true;
}
