// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "TimeAbilityComponent.generated.h"

class UTimeableComponent;
class UTimeSandComponent;

/** What the player is currently aiming at, as far as Freeze is concerned */
UENUM(BlueprintType)
enum class EFreezeAimState : uint8
{
	NoTarget,
	Affordable,
	Unaffordable
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnFreezeAimStateChanged, EFreezeAimState, AimState);

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

	/** How far from the camera Freeze can reach */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Freeze", meta=(ClampMin="0"))
	float FreezeRange = 3000.0f;

	/** Sand that Rewind will cost. Unused until Rewind exists, but the HUD already shows it */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Rewind", meta=(ClampMin="0"))
	float RewindSandCost = 35.0f;

	/** Called when the aim state changes */
	UPROPERTY(BlueprintAssignable, Category="Freeze")
	FOnFreezeAimStateChanged OnFreezeAimStateChanged;

	/** Constructor */
	UTimeAbilityComponent();

	/** Freezes whatever the player is aiming at */
	UFUNCTION(BlueprintCallable, Category="Freeze")
	bool TryFreeze();

	/** Freezes the target, charging sand only if the freeze actually happens */
	UFUNCTION(BlueprintCallable, Category="Freeze")
	bool TryFreezeTarget(UTimeableComponent* Target);

	UFUNCTION(BlueprintPure, Category="Freeze")
	EFreezeAimState GetAimState() const { return AimState; }

protected:

	/** Traces the player's aim and reports aim state changes */
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

private:

	/** Returns the freezable target under the crosshair, if any */
	UTimeableComponent* FindAimTarget() const;

	EFreezeAimState AimState = EFreezeAimState::NoTarget;
};
