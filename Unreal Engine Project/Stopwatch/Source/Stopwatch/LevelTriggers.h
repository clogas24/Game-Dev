// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LevelTriggers.generated.h"

class AStopwatchCharacter;
class AStopwatchGameMode;
class UBoxComponent;
class UMaterialInstanceDynamic;
class UPointLightComponent;
class UStaticMeshComponent;

/**
 *  A box that reacts to the living player walking into it
 *  Stands on the actor's location: put the actor on the floor and size the box with its extent
 */
UCLASS(Abstract)
class APlayerTrigger : public AActor
{
	GENERATED_BODY()

	UPROPERTY(VisibleAnywhere, Category="Components")
	UBoxComponent* Box;

public:

	/** Constructor */
	APlayerTrigger();

	FORCEINLINE UBoxComponent* GetBox() const { return Box; }

	/** For a player who was inside from the start: spawning inside a box doesn't count as walking into it */
	void NotifyPlayerInside(AStopwatchCharacter* Character) { OnPlayerEnter(Character); }

	/** Keeps the box standing on the actor's location */
	virtual void OnConstruction(const FTransform& Transform) override;

	/** Again, for a box resized since construction */
	virtual void BeginPlay() override;

protected:

	/** Called when the player enters the box */
	virtual void OnPlayerEnter(AStopwatchCharacter* Character) {}

	AStopwatchGameMode* GetStopwatchGameMode() const;

private:

	UFUNCTION()
	void OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);
};

/**
 *  Where the player respawns. A greybox pedestal that lights up green when reached
 *  Only advances: reaching a checkpoint that comes before the current one does nothing
 *  The player respawns on the actor's location, facing its yaw
 */
UCLASS()
class ACheckpoint : public APlayerTrigger
{
	GENERATED_BODY()

	/** No collision, so it doesn't trip anyone */
	UPROPERTY(VisibleAnywhere, Category="Components")
	UStaticMeshComponent* Pedestal;

	UPROPERTY(VisibleAnywhere, Category="Components")
	UPointLightComponent* Light;

public:

	/** Section this checkpoint is in. Respawning here resets that section's IResettable pieces */
	UPROPERTY(EditAnywhere, Category="Checkpoint", meta=(ClampMin="0"))
	int32 SectionId = 1;

	/** Orders the checkpoints within a section */
	UPROPERTY(EditAnywhere, Category="Checkpoint", meta=(ClampMin="0"))
	int32 OrderInSection = 0;

	/** Sand is topped up to at least this on respawn here */
	UPROPERTY(EditAnywhere, Category="Checkpoint", meta=(ClampMin="0"))
	float MinSandOnRespawn = 60.0f;

	/** Brightness of the light once reached */
	UPROPERTY(EditAnywhere, Category="Checkpoint", meta=(ClampMin="0"))
	float LitIntensity = 8000.0f;

	/** Constructor */
	ACheckpoint();

	/** True if this comes later in the level than Other */
	bool IsAfter(const ACheckpoint* Other) const;

	void SetLit(bool bLit);
	bool IsLit() const { return bIsLit; }

protected:

	virtual void BeginPlay() override;
	virtual void OnPlayerEnter(AStopwatchCharacter* Character) override;

private:

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> PedestalMaterial;

	bool bIsLit = false;
};

/**
 *  Marks the start of a section: its name shows on the HUD, and the playtest log starts a new row
 */
UCLASS()
class ASectionTrigger : public APlayerTrigger
{
	GENERATED_BODY()

public:

	UPROPERTY(EditAnywhere, Category="Section", meta=(ClampMin="0"))
	int32 SectionId = 1;

	UPROPERTY(EditAnywhere, Category="Section")
	FText DisplayName;

protected:

	virtual void OnPlayerEnter(AStopwatchCharacter* Character) override;
};

/**
 *  Shows a prompt on the HUD, once per session, while the game is being played
 *  {freeze}, {rewind} and {jump} in the prompt become the keys for the device used last
 */
UCLASS()
class ATutorialTrigger : public APlayerTrigger
{
	GENERATED_BODY()

public:

	UPROPERTY(EditAnywhere, Category="Tutorial", meta=(MultiLine="true"))
	FText Prompt;

	/** Seconds the prompt stays up */
	UPROPERTY(EditAnywhere, Category="Tutorial", meta=(ClampMin="0.5"))
	float DisplaySeconds = 10.0f;

	/** What the prompt is about. Tests check it's in view from the trigger */
	UPROPERTY(EditAnywhere, Category="Tutorial")
	TObjectPtr<AActor> FocusTarget;

protected:

	virtual void OnPlayerEnter(AStopwatchCharacter* Character) override;
};

/**
 *  The end of the level: wins the game
 */
UCLASS()
class AGoalTrigger : public APlayerTrigger
{
	GENERATED_BODY()

protected:

	virtual void OnPlayerEnter(AStopwatchCharacter* Character) override;
};

/**
 *  Starts the collapse countdown. Other deaths keep the time left; running out restarts it from the respawn
 */
UCLASS()
class ACollapseTrigger : public APlayerTrigger
{
	GENERATED_BODY()

public:

	/** Seconds the player has to reach the goal */
	UPROPERTY(EditAnywhere, Category="Collapse", meta=(ClampMin="1"))
	float CollapseSeconds = 120.0f;

protected:

	virtual void OnPlayerEnter(AStopwatchCharacter* Character) override;
};
