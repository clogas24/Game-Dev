// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "TimeSandComponent.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnSandChanged, float, Current, float, Max);
DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnSpendFailed);

/**
 *  The resource spent by the player's time abilities
 */
UCLASS(ClassGroup="Stopwatch", meta=(BlueprintSpawnableComponent))
class UTimeSandComponent : public UActorComponent
{
	GENERATED_BODY()

public:

	/** Maximum amount of sand */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Time Sand", meta=(ClampMin="0"))
	float MaxSand = 100.0f;

	/** Sand regained per second of undilated time. Playtest knob, 0 means no regen */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Time Sand", meta=(ClampMin="0"))
	float RegenPerSecond = 0.0f;

	/** Called whenever the amount of sand changes */
	UPROPERTY(BlueprintAssignable, Category="Time Sand")
	FOnSandChanged OnSandChanged;

	/** Called when a spend is refused because it can't be afforded */
	UPROPERTY(BlueprintAssignable, Category="Time Sand")
	FOnSpendFailed OnSpendFailed;

	/** Constructor */
	UTimeSandComponent();

	/** Spends the amount if affordable. Otherwise spends nothing and fires OnSpendFailed */
	UFUNCTION(BlueprintCallable, Category="Time Sand")
	bool TrySpend(float Amount);

	/** Adds sand, clamped to MaxSand */
	UFUNCTION(BlueprintCallable, Category="Time Sand")
	void Add(float Amount);

	/** Sets the amount of sand, clamped between 0 and MaxSand */
	UFUNCTION(BlueprintCallable, Category="Time Sand")
	void SetSand(float NewSand);

	UFUNCTION(BlueprintPure, Category="Time Sand")
	float GetCurrentSand() const { return CurrentSand; }

	UFUNCTION(BlueprintPure, Category="Time Sand")
	bool CanAfford(float Amount) const { return Amount <= CurrentSand; }

	UFUNCTION(BlueprintPure, Category="Time Sand")
	bool IsFull() const { return CurrentSand >= MaxSand; }

	// Checkpoint hook (next step): on respawn, top the player up to a minimum so they can't softlock.
	// UPROPERTY(EditAnywhere, Category="Time Sand") float MinSandOnRespawn = 25.0f;
	// void ApplyRespawnMinimum() { SetSand(FMath::Max(CurrentSand, MinSandOnRespawn)); }

protected:

	/** Fills the sand when gameplay starts */
	virtual void BeginPlay() override;

	/** Applies regen */
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

private:

	/** Current amount of sand */
	UPROPERTY(VisibleInstanceOnly, Category="Time Sand")
	float CurrentSand = 100.0f;
};
