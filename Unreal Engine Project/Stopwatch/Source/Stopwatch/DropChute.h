// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "PhysicsProp.h"
#include "TimeStateProvider.h"
#include "DropChute.generated.h"

class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  Hangs in the air and releases an APhysicsProp every Interval seconds, or when ReleaseProp is called
 */
UCLASS()
class ADropChute : public AActor, public ITimeStateProvider
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Mesh;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	/** Seconds between releases. 0 means only on trigger */
	UPROPERTY(EditAnywhere, Category="Chute", meta=(ClampMin="0"))
	float Interval = 4.0f;

	UPROPERTY(EditAnywhere, Category="Chute")
	EPropShape PropShape = EPropShape::Cube;

	UPROPERTY(EditAnywhere, Category="Chute", meta=(ClampMin="0.1"))
	float PropMass = 200.0f;

	/** Actor scale of each prop */
	UPROPERTY(EditAnywhere, Category="Chute")
	FVector PropScale = FVector(1.5f, 1.5f, 0.5f);

	/** Oldest props are removed past this, unless time is being messed with */
	UPROPERTY(EditAnywhere, Category="Chute", meta=(ClampMin="1"))
	int32 MaxLiveProps = 3;

	/** Constructor */
	ADropChute();

	/** Counts down to the next release */
	virtual void Tick(float DeltaSeconds) override;

	/** Rewinding the countdown is all it takes: props that were already released keep their own history */
	virtual float CaptureTimeState() const override { return Elapsed; }
	virtual void RestoreTimeState(float State) override { Elapsed = State; }

	/** Releases a prop now. Skipped if every live prop is in use and none can be removed */
	UFUNCTION(BlueprintCallable, Category="Chute")
	void ReleaseProp();

	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }
	const TArray<TWeakObjectPtr<APhysicsProp>>& GetLiveProps() const { return LiveProps; }

private:

	TArray<TWeakObjectPtr<APhysicsProp>> LiveProps;

	/** Dilated seconds since the last release */
	float Elapsed = 0.0f;
};
