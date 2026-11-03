// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "RollTrigger.generated.h"

class ADropChute;
class APhysicsProp;
class UBoxComponent;

/**
 *  When the player enters, wakes and pushes a prop and releases a chute's prop
 *  Not timeable: there's nothing to see or aim at
 */
UCLASS()
class ARollTrigger : public AActor
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UBoxComponent* Box;

public:

	/** Prop to wake, if any */
	UPROPERTY(EditAnywhere, Category="Trigger")
	TObjectPtr<APhysicsProp> TargetProp;

	/** Velocity the prop wakes up with */
	UPROPERTY(EditAnywhere, Category="Trigger")
	FVector PushVelocity = FVector::ZeroVector;

	/** Chute to release, if any */
	UPROPERTY(EditAnywhere, Category="Trigger")
	TObjectPtr<ADropChute> TargetChute;

	/** Constructor */
	ARollTrigger();


private:

	UFUNCTION()
	void OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);
};
