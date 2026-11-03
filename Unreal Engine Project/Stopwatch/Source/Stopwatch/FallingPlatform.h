// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "TimeStateProvider.h"
#include "FallingPlatform.generated.h"

class UBoxComponent;
class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  Kinematic until stepped on, then shakes and drops. Rewinding brings it back and holds it there. Scale the actor to size it
 */
UCLASS()
class AFallingPlatform : public AActor, public ITimeStateProvider
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Mesh;

	/** Sits on top of the mesh and detects the player */
	UPROPERTY(VisibleAnywhere, Category="Components")
	UBoxComponent* StepTrigger;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	/** Seconds of shaking before the drop */
	UPROPERTY(EditAnywhere, Category="Platform", meta=(ClampMin="0"))
	float ShakeTime = 1.0f;

	/** How far the shake moves the platform */
	UPROPERTY(EditAnywhere, Category="Platform", meta=(ClampMin="0"))
	float ShakeDistance = 5.0f;

	/** Constructor */
	AFallingPlatform();

	/** Watches for the player and shakes */
	virtual void Tick(float DeltaSeconds) override;

	/** The transform is already recorded, so only the shake clock needs restoring */
	virtual float CaptureTimeState() const override { return ShakeElapsed; }
	virtual void RestoreTimeState(float State) override { ShakeElapsed = State; }

	/** Starts the shake as if the player had stepped on */
	void StartShaking();

	FORCEINLINE UStaticMeshComponent* GetMesh() const { return Mesh; }
	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }

protected:

	/** Records the resting location */
	virtual void BeginPlay() override;

private:

	FVector HomeLocation = FVector::ZeroVector;

	/** Dilated seconds of shaking so far. 0 means it hasn't been stepped on */
	float ShakeElapsed = 0.0f;
	bool bStepped = false;
};
