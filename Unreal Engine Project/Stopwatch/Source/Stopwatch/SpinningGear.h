// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "TimeStateProvider.h"
#include "SpinningGear.generated.h"

class UBoxComponent;
class UStaticMesh;
class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  A rotating disc the player can ride, or with bIsBlade a rotating arm that kills on contact
 */
UCLASS()
class ASpinningGear : public AActor, public ITimeStateProvider
{
	GENERATED_BODY()

	/** The point everything rotates around */
	UPROPERTY(VisibleAnywhere, Category="Components")
	USceneComponent* Rotor;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Mesh;

	/** Surrounds the arm, for bIsBlade */
	UPROPERTY(VisibleAnywhere, Category="Components")
	UBoxComponent* KillZone;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	/** Rotation speed, in degrees per second */
	UPROPERTY(EditAnywhere, Category="Gear")
	float Speed = 90.0f;

	/** Axis of rotation, relative to the actor */
	UPROPERTY(EditAnywhere, Category="Gear")
	FVector Axis = FVector::UpVector;

	/** Radius of the disc, or half the length of the arm */
	UPROPERTY(EditAnywhere, Category="Gear", meta=(ClampMin="1"))
	float Radius = 400.0f;

	/** Thickness of the disc or arm */
	UPROPERTY(EditAnywhere, Category="Gear", meta=(ClampMin="1"))
	float Thickness = 40.0f;

	UPROPERTY(EditAnywhere, Category="Gear")
	bool bIsBlade = false;

	/** Constructor */
	ASpinningGear();

	/** Advances the rotation */
	virtual void Tick(float DeltaSeconds) override;

	/** Picks the shape and poses the rotation */
	virtual void OnConstruction(const FTransform& Transform) override;

	virtual float CaptureTimeState() const override { return Elapsed; }
	virtual void RestoreTimeState(float State) override;

	/** Returns the current rotation, in degrees */
	float GetAngle() const { return Speed * Elapsed; }

	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }

protected:

	/** Applies the settings that changed since the level was saved */
	virtual void BeginPlay() override;

private:

	void ApplySettings();
	void UpdatePose();

	UPROPERTY()
	TObjectPtr<UStaticMesh> CubeMesh;

	UPROPERTY()
	TObjectPtr<UStaticMesh> CylinderMesh;

	/** Dilated seconds so far */
	float Elapsed = 0.0f;
};
