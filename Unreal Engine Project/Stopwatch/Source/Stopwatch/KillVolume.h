// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "StopwatchCharacter.h"
#include "KillVolume.generated.h"

class UBoxComponent;

/**
 *  Kills the player on entry. Other actors pass through
 */
UCLASS()
class AKillVolume : public AActor
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UBoxComponent* Box;

public:

	/** What the player dies of */
	UPROPERTY(EditAnywhere, Category="Kill Volume")
	EDeathCause Cause = EDeathCause::Fall;

	/** Constructor */
	AKillVolume();


private:

	UFUNCTION()
	void OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);
};
