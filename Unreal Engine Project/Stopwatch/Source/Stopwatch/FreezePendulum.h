// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Resettable.h"
#include "TimeStateProvider.h"
#include "FreezePendulum.generated.h"

class USphereComponent;
class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  Swinging Freeze test target. Hangs down from its actor location
 *  Its swing is driven by the dilated delta, so a freeze stops it and it resumes from the same angle
 */
UCLASS()
class AFreezePendulum : public AActor, public ITimeStateProvider, public IResettable
{
	GENERATED_BODY()

	/** The point the arm swings around */
	UPROPERTY(VisibleAnywhere, Category="Components")
	USceneComponent* Pivot;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Arm;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Bob;

	/** Surrounds the bob, for bKillsPlayer */
	UPROPERTY(VisibleAnywhere, Category="Components")
	USphereComponent* KillZone;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	/** Section whose checkpoints reset this piece. 0 means none */
	UPROPERTY(EditAnywhere, Category="Section")
	int32 SectionId = 0;

	virtual int32 GetSectionId() const override { return SectionId; }

	/** Back to how the level started */
	virtual void ResetToStart() override;

	/** Largest swing angle, in degrees */
	UPROPERTY(EditAnywhere, Category="Pendulum")
	float Amplitude = 60.0f;

	/** Seconds for a full swing */
	UPROPERTY(EditAnywhere, Category="Pendulum", meta=(ClampMin="0.01"))
	float Period = 2.5f;

	/** Length of the arm */
	UPROPERTY(EditAnywhere, Category="Pendulum", meta=(ClampMin="1"))
	float ArmLength = 300.0f;

	/** Starting point in the swing, in radians */
	UPROPERTY(EditAnywhere, Category="Pendulum")
	float PhaseOffset = 0.0f;

	/** Axis the arm rotates around, relative to the actor */
	UPROPERTY(EditAnywhere, Category="Pendulum")
	FVector SwingAxis = FVector::ForwardVector;

	/** Makes this a blade: touching the bob while it swings kills the player */
	UPROPERTY(EditAnywhere, Category="Pendulum")
	bool bKillsPlayer = false;

	/** Reach of the kill zone around the bob's center */
	UPROPERTY(EditAnywhere, Category="Pendulum", meta=(ClampMin="1"))
	float KillRadius = 60.0f;

	/** Constructor */
	AFreezePendulum();

	/** Advances the swing */
	virtual void Tick(float DeltaSeconds) override;

	/** Applies the settings that changed since the level was saved */
	virtual void BeginPlay() override;

	/** Sizes the arm and poses the swing */
	virtual void OnConstruction(const FTransform& Transform) override;

	virtual float CaptureTimeState() const override { return Elapsed; }
	virtual void RestoreTimeState(float State) override;

	/** Returns the current swing angle, in degrees */
	float GetAngle() const;

	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }

private:

	/** Sizes the arm and kill zone */
	void ApplySettings();

	/** Rotates the pivot to the current angle */
	void UpdateSwing();

	/** Dilated seconds of swinging so far */
	float Elapsed = 0.0f;
};
