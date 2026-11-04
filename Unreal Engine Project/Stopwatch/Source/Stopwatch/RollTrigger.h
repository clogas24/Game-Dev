// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "RollTrigger.generated.h"

class ADropChute;
class APhysicsProp;
class UBoxComponent;
class UMaterialInstanceDynamic;
class UStaticMeshComponent;

/**
 *  A pressure plate on the floor. Stepping on wakes and pushes a prop and releases a chute's prop
 *  Place the actor on the floor. Not timeable, so it isn't brass
 */
UCLASS()
class ARollTrigger : public AActor
{
	GENERATED_BODY()

	/** The plate the player sees. No collision, so it doesn't trip anyone */
	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Plate;

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

	/** Eases the plate up or down */
	virtual void Tick(float DeltaSeconds) override;

	bool IsPressed() const { return PressCount > 0; }

protected:

	/** Colors the plate */
	virtual void BeginPlay() override;

private:

	UFUNCTION()
	void OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION()
	void OnBoxEndOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex);

	/** Red while waiting, green while pressed */
	void UpdateColor();

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> PlateMaterial;

	int32 PressCount = 0;
};
