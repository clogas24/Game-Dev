// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "BrokenBridge.generated.h"

class UStaticMeshComponent;
class UTimeableComponent;

/**
 *  Planks placed in a scattered fallen pose. Rewinding reassembles them into a bridge running along the actor's X axis
 *  The hold time and speeds are on the Timeable component
 */
UCLASS()
class ABrokenBridge : public AActor
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UTimeableComponent* Timeable;

public:

	/** Length of the intact bridge, starting at the actor */
	UPROPERTY(EditAnywhere, Category="Bridge", meta=(ClampMin="1"))
	float BridgeLength = 1500.0f;

	UPROPERTY(EditAnywhere, Category="Bridge", meta=(ClampMin="1"))
	int32 PlankCount = 10;

	UPROPERTY(EditAnywhere, Category="Bridge", meta=(ClampMin="1"))
	float PlankWidth = 400.0f;

	UPROPERTY(EditAnywhere, Category="Bridge", meta=(ClampMin="1"))
	float PlankThickness = 30.0f;

	/** Fallen planks lie this far below the bridge. Put a floor there so the player can see and aim at them */
	UPROPERTY(EditAnywhere, Category="Bridge", meta=(ClampMin="0"))
	float FallDepth = 500.0f;

	/** How far the fallen planks scatter around where they belong */
	UPROPERTY(EditAnywhere, Category="Bridge", meta=(ClampMin="0"))
	float Scatter = 250.0f;

	/** Picks the fallen pose */
	UPROPERTY(EditAnywhere, Category="Bridge")
	int32 Seed = 1;

	/** Constructor */
	ABrokenBridge();

	/** Builds the planks for the editor */
	virtual void OnConstruction(const FTransform& Transform) override;

	FORCEINLINE UTimeableComponent* GetTimeable() const { return Timeable; }
	const TArray<TObjectPtr<UStaticMeshComponent>>& GetPlanks() const { return Planks; }

protected:

	/** Builds the planks for play, before Timeable captures their fallen pose */
	virtual void BeginPlay() override;

private:

	void BuildPlanks();

	UPROPERTY()
	TObjectPtr<UStaticMesh> CubeMesh;

	/** Generated from the settings, so never saved with the level */
	UPROPERTY(Transient)
	TArray<TObjectPtr<UStaticMeshComponent>> Planks;
};
