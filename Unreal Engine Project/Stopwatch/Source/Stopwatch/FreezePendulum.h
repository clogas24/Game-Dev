// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "FreezePendulum.generated.h"

class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  Swinging Freeze test target. Hangs down from its actor location
 *  Its swing is driven by the dilated delta, so a freeze stops it and it resumes from the same angle
 */
UCLASS()
class AFreezePendulum : public AActor
{
	GENERATED_BODY()

	/** The point the arm swings around */
	UPROPERTY(VisibleAnywhere, Category="Components")
	USceneComponent* Pivot;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Arm;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Bob;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

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

	/** Constructor */
	AFreezePendulum();

	/** Advances the swing */
	virtual void Tick(float DeltaSeconds) override;

	/** Sizes the arm and poses the swing */
	virtual void OnConstruction(const FTransform& Transform) override;

	/** Returns the current swing angle, in degrees */
	float GetAngle() const;

	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }

private:

	/** Rotates the pivot to the current angle */
	void UpdateSwing();

	/** Dilated seconds of swinging so far */
	float Elapsed = 0.0f;
};
