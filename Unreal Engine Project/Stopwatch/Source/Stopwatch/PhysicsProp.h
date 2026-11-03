// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "PhysicsProp.generated.h"

class UStaticMesh;
class UStaticMeshComponent;
class UTimeableComponent;

UENUM(BlueprintType)
enum class EPropShape : uint8
{
	Cube,
	Sphere
};

/**
 *  Timeable physics object. Scale the actor to size it
 */
UCLASS()
class APhysicsProp : public AActor
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Mesh;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	UPROPERTY(EditAnywhere, Category="Prop")
	EPropShape Shape = EPropShape::Cube;

	/** Mass in kg. The default is twice the default character's 100 kg, so standing on it doesn't shove it around */
	UPROPERTY(EditAnywhere, Category="Prop", meta=(ClampMin="0.1"))
	float Mass = 200.0f;

	/** Resistance to spinning. High values stop a box from tumbling, so it stays usable as a step */
	UPROPERTY(EditAnywhere, Category="Prop", meta=(ClampMin="0"))
	float AngularDamping = 0.0f;

	/** Stay kinematic until Wake is called, so a prop can wait on a ramp or a ledge */
	UPROPERTY(EditAnywhere, Category="Prop")
	bool bHeldUntilWoken = false;

	/** Seconds between teleports back to the spawn transform. 0 means never */
	UPROPERTY(EditAnywhere, Category="Prop", meta=(ClampMin="0"))
	float AutoResetInterval = 0.0f;

	/** Constructor */
	APhysicsProp();

	/** Applies the shape and mass */
	virtual void OnConstruction(const FTransform& Transform) override;

	/** Starts simulating and sets the velocity, unless time is being messed with */
	UFUNCTION(BlueprintCallable, Category="Prop")
	void Wake(FVector Velocity);

	/** Teleports back to the spawn transform with no velocity, unless frozen or rewinding */
	void AutoReset();

	FORCEINLINE UStaticMeshComponent* GetMesh() const { return Mesh; }
	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }

protected:

	/** Records the spawn transform and starts the reset timer */
	virtual void BeginPlay() override;

private:

	void ApplySettings();

	/** True when Timeable owns this prop's physics state */
	bool IsTimeLocked() const;

	UPROPERTY()
	TObjectPtr<UStaticMesh> CubeMesh;

	UPROPERTY()
	TObjectPtr<UStaticMesh> SphereMesh;

	FTransform SpawnTransform;
	FTimerHandle AutoResetTimer;
};
