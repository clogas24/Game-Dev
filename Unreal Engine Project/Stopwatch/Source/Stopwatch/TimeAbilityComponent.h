// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "TimeAbilityComponent.generated.h"

class UTimeableComponent;
class UTimeSandComponent;

/** What the player can do to what they're currently aiming at */
UENUM(BlueprintType)
enum class ETimeAimState : uint8
{
	NoTarget,

	/** Can be frozen, and maybe rewound too */
	CanFreeze,

	RewindOnly,

	/** A valid target, but the player can't pay for anything it allows */
	Unaffordable
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnAimStateChanged, ETimeAimState, AimState);
DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnTimeAbilityUsed);

/**
 *  The player's time abilities, paid for with the owner's UTimeSandComponent
 */
UCLASS(ClassGroup="Stopwatch", meta=(BlueprintSpawnableComponent))
class UTimeAbilityComponent : public UActorComponent
{
	GENERATED_BODY()

public:

	/** Sand charged for each successful Freeze, including refreezes */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Freeze", meta=(ClampMin="0"))
	float FreezeSandCost = 25.0f;

	/** How long a Freeze lasts, in seconds */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Freeze", meta=(ClampMin="0.1"))
	float FreezeDuration = 5.0f;

	/** How far from the camera Freeze and Rewind can reach */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Freeze", meta=(ClampMin="0"))
	float FreezeRange = 3000.0f;

	/** Sand charged when a rewind actually starts */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Rewind", meta=(ClampMin="0"))
	float RewindSandCost = 35.0f;

	/** Called when the aim state changes */
	UPROPERTY(BlueprintAssignable, Category="Freeze")
	FOnAimStateChanged OnAimStateChanged;

	/** Called when a Freeze or Rewind succeeds */
	UPROPERTY(BlueprintAssignable, Category="Freeze")
	FOnTimeAbilityUsed OnAbilityUsed;

	/** Constructor */
	UTimeAbilityComponent();

	/** Freezes whatever the player is aiming at */
	UFUNCTION(BlueprintCallable, Category="Freeze")
	bool TryFreeze();

	/** Freezes the target, charging sand only if the freeze actually happens */
	UFUNCTION(BlueprintCallable, Category="Freeze")
	bool TryFreezeTarget(UTimeableComponent* Target);

	/** Rewinds whatever the player is aiming at */
	UFUNCTION(BlueprintCallable, Category="Rewind")
	bool TryRewind();

	/** Rewinds the target, charging sand only if the rewind actually starts */
	UFUNCTION(BlueprintCallable, Category="Rewind")
	bool TryRewindTarget(UTimeableComponent* Target);

	UFUNCTION(BlueprintPure, Category="Freeze")
	ETimeAimState GetAimState() const { return AimState; }

	/** Returns the target under the crosshair as of the last tick, if any */
	UTimeableComponent* GetAimTarget() const { return AimTarget.Get(); }

protected:

	/** Traces the player's aim and reports aim state changes */
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

private:

	/** Returns the timeable target under the crosshair, if any */
	UTimeableComponent* FindAimTarget() const;

	ETimeAimState AimState = ETimeAimState::NoTarget;
	TWeakObjectPtr<UTimeableComponent> AimTarget;
};
