// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "TimeStateProvider.h"
#include "MovingPlatform.generated.h"

class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  A platform that ping-pongs between local points, easing at each one, and carries the player
 */
UCLASS()
class AMovingPlatform : public AActor, public ITimeStateProvider
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Mesh;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	/** Points to travel between, relative to the actor */
	UPROPERTY(EditAnywhere, Category="Platform", meta=(MakeEditWidget))
	TArray<FVector> Points = { FVector::ZeroVector, FVector(1000.0f, 0.0f, 0.0f) };

	/** Seconds to travel between two neighboring points */
	UPROPERTY(EditAnywhere, Category="Platform", meta=(ClampMin="0.01"))
	float SegmentTime = 3.0f;

	/** Width, depth and height of the platform */
	UPROPERTY(EditAnywhere, Category="Platform")
	FVector Size = FVector(400.0f, 400.0f, 40.0f);

	/** Constructor */
	AMovingPlatform();

	/** Advances along the path */
	virtual void Tick(float DeltaSeconds) override;

	/** Sizes the platform and poses it on the path */
	virtual void OnConstruction(const FTransform& Transform) override;

	virtual float CaptureTimeState() const override { return Elapsed; }
	virtual void RestoreTimeState(float State) override;

	/** Returns the position along the path at this point in time */
	FVector GetPathLocation(float Time) const;

	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }

protected:

	/** Applies the settings that changed since the level was saved */
	virtual void BeginPlay() override;

private:

	void ApplySettings();
	void UpdatePose();

	/** Dilated seconds so far */
	float Elapsed = 0.0f;
};
