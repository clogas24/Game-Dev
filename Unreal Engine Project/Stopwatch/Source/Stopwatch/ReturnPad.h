// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "ReturnPad.generated.h"

class UBoxComponent;
class UStaticMeshComponent;

/**
 *  A glowing pad on the floor that teleports the player to Destination. Place the actor on the floor
 */
UCLASS()
class AReturnPad : public AActor
{
	GENERATED_BODY()

	/** No collision, so it doesn't trip anyone */
	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Pad;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UBoxComponent* Box;

public:

	/** World location the player's feet arrive at */
	UPROPERTY(EditAnywhere, Category="Pad")
	FVector Destination = FVector::ZeroVector;

	/** Which way the player faces on arrival */
	UPROPERTY(EditAnywhere, Category="Pad")
	float DestinationYaw = 0.0f;

	/** Constructor */
	AReturnPad();

protected:

	/** Makes the pad glow */
	virtual void BeginPlay() override;

private:

	UFUNCTION()
	void OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);
};
