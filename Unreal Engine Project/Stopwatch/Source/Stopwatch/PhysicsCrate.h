// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "PhysicsCrate.generated.h"

class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  Physics Freeze test target, heavy enough for the player to stand on
 */
UCLASS()
class APhysicsCrate : public AActor
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Mesh;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	/** Seconds between teleports back to the spawn transform. 0 means never */
	UPROPERTY(EditAnywhere, Category="Crate", meta=(ClampMin="0"))
	float AutoResetInterval = 0.0f;

	/** Constructor */
	APhysicsCrate();

	/** Teleports back to the spawn transform with no velocity, unless frozen */
	void AutoReset();

	FORCEINLINE UStaticMeshComponent* GetMesh() const { return Mesh; }
	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }

protected:

	/** Records the spawn transform and starts the reset timer */
	virtual void BeginPlay() override;

private:

	FTransform SpawnTransform;
	FTimerHandle AutoResetTimer;
};
