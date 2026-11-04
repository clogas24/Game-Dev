// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HourglassPickup.generated.h"

class USphereComponent;
class UStaticMeshComponent;
class UTimeSandComponent;

/**
 *  Spinning pickup that refills the player's Time Sand
 *  Ignored while the player's sand is full, so it can't be wasted
 */
UCLASS()
class AHourglassPickup : public AActor
{
	GENERATED_BODY()

	/** Overlap trigger */
	UPROPERTY(VisibleAnywhere, Category="Components")
	USphereComponent* Trigger;

	/** Placeholder mesh */
	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Mesh;

public:

	/** Sand given to the player */
	UPROPERTY(EditAnywhere, Category="Pickup", meta=(ClampMin="0"))
	float SandAmount = 35.0f;

	/** Seconds until the pickup reappears. 0 means it's consumed forever */
	UPROPERTY(EditAnywhere, Category="Pickup", meta=(ClampMin="0"))
	float RespawnTime = 0.0f;

	/** Spin speed, in degrees per second */
	UPROPERTY(EditAnywhere, Category="Pickup")
	float SpinSpeed = 45.0f;

	/** Bob distance from the resting position */
	UPROPERTY(EditAnywhere, Category="Pickup")
	float BobHeight = 15.0f;

	/** Constructor */
	AHourglassPickup();

protected:

	/** Makes the mesh glow the sand's color */
	virtual void BeginPlay() override;

	/** Spins and bobs the mesh */
	virtual void Tick(float DeltaTime) override;

private:

	UFUNCTION()
	void OnTriggerBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

	UFUNCTION()
	void OnTriggerEndOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex);

	/** Gives the sand and consumes the pickup */
	void Collect(UTimeSandComponent* Sand);

	/** Brings a consumed pickup back */
	void Respawn();

	/** Player standing in the trigger with full sand, collected as soon as they have room */
	TWeakObjectPtr<UTimeSandComponent> WaitingSand;

	float BobTime = 0.0f;
	FTimerHandle RespawnTimer;
};
