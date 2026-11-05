// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Resettable.h"
#include "TimeStateProvider.h"
#include "Crusher.generated.h"

class UBoxComponent;
class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  A piston cycling rise, hold and slam. Place the actor on the floor it slams onto
 */
UCLASS()
class ACrusher : public AActor, public ITimeStateProvider, public IResettable
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Head;

	/** Sits just under the head, and kills while slamming */
	UPROPERTY(VisibleAnywhere, Category="Components")
	UBoxComponent* KillZone;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	/** Section whose checkpoints reset this piece. 0 means none */
	UPROPERTY(EditAnywhere, Category="Section")
	int32 SectionId = 0;

	virtual int32 GetSectionId() const override { return SectionId; }

	/** Back to how the level started */
	virtual void ResetToStart() override;

	/** Width, depth and height of the head */
	UPROPERTY(EditAnywhere, Category="Crusher")
	FVector HeadSize = FVector(300.0f, 300.0f, 150.0f);

	/** How high the bottom of the head rises above the actor */
	UPROPERTY(EditAnywhere, Category="Crusher", meta=(ClampMin="1"))
	float TravelHeight = 400.0f;

	UPROPERTY(EditAnywhere, Category="Crusher", meta=(ClampMin="0.01"))
	float RiseTime = 1.5f;

	UPROPERTY(EditAnywhere, Category="Crusher", meta=(ClampMin="0"))
	float HoldTime = 1.5f;

	UPROPERTY(EditAnywhere, Category="Crusher", meta=(ClampMin="0.01"))
	float SlamTime = 0.3f;

	/** Starting point in the cycle, in seconds */
	UPROPERTY(EditAnywhere, Category="Crusher", meta=(ClampMin="0"))
	float PhaseOffset = 0.0f;

	/** Constructor */
	ACrusher();

	/** Advances the cycle */
	virtual void Tick(float DeltaSeconds) override;

	/** Sizes the head and poses the cycle */
	virtual void OnConstruction(const FTransform& Transform) override;

	virtual float CaptureTimeState() const override { return Elapsed; }
	virtual void RestoreTimeState(float State) override;

	/** Returns how raised the head is at this point in the cycle: 0 is down, 1 is up */
	float GetHeightFraction(float Time) const;

	/** Returns true if the head is coming down at this point in the cycle */
	bool IsSlamming(float Time) const;

	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }

protected:

	/** Applies the settings that changed since the level was saved */
	virtual void BeginPlay() override;

private:

	void ApplySettings();

	/** Moves the head to the current height */
	void UpdatePose();

	/** Dilated seconds so far */
	float Elapsed = 0.0f;
};
