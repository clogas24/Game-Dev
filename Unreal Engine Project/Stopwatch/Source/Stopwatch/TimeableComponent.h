// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "TimeableComponent.generated.h"

class UMaterialInterface;
class UMeshComponent;
class UPrimitiveComponent;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnFrozenChanged, bool, bFrozen);

/** A simulating body's velocity, captured when it was frozen */
USTRUCT()
struct FTimeableSavedBody
{
	GENERATED_BODY()

	UPROPERTY()
	TWeakObjectPtr<UPrimitiveComponent> Body;

	FVector LinearVelocity = FVector::ZeroVector;
	FVector AngularVelocity = FVector::ZeroVector;
};

/** A mesh's overlay material, captured when it was frozen */
USTRUCT()
struct FTimeableSavedOverlay
{
	GENERATED_BODY()

	UPROPERTY()
	TWeakObjectPtr<UMeshComponent> Mesh;

	UPROPERTY()
	TObjectPtr<UMaterialInterface> Overlay;
};

/**
 *  Add to an actor to make it a target for the player's time abilities
 *  Freezing stops the actor's time and physics, leaving its collision on, then resumes where it left off
 */
UCLASS(ClassGroup="Stopwatch", meta=(BlueprintSpawnableComponent))
class UTimeableComponent : public UActorComponent
{
	GENERATED_BODY()

public:

	/** Set to false to make this actor immune to Freeze */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Freeze")
	bool bCanBeFrozen = true;

	/** Overlay shown on every mesh of the actor while frozen */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Freeze")
	TSoftObjectPtr<UMaterialInterface> FrozenOverlayMaterial;

	/** Called when the actor freezes or resumes, but not when a freeze is only extended */
	UPROPERTY(BlueprintAssignable, Category="Freeze")
	FOnFrozenChanged OnFrozenChanged;

	/** Constructor */
	UTimeableComponent();

	UFUNCTION(BlueprintPure, Category="Freeze")
	virtual bool CanBeFrozen() const { return bCanBeFrozen; }

	UFUNCTION(BlueprintPure, Category="Freeze")
	bool IsFrozen() const { return bIsFrozen; }

	/** Freezes the actor. Freezing it again restarts the duration, which counts down in world time */
	UFUNCTION(BlueprintCallable, Category="Freeze")
	virtual void Freeze(float Duration);

	/** Resumes the actor, restoring its velocities and overlays */
	UFUNCTION(BlueprintCallable, Category="Freeze")
	virtual void Unfreeze();

	/** Clears the timer and restores the actor */
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

private:

	void SaveAndStopBodies();
	void RestoreBodies();
	void SaveAndApplyOverlays();
	void RestoreOverlays();

	UPROPERTY(Transient)
	TArray<FTimeableSavedBody> SavedBodies;

	UPROPERTY(Transient)
	TArray<FTimeableSavedOverlay> SavedOverlays;

	bool bIsFrozen = false;
	float SavedTimeDilation = 1.0f;
	FTimerHandle UnfreezeTimer;
};
