// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Camera/CameraShakeBase.h"
#include "GameFramework/GameModeBase.h"
#include "StopwatchCharacter.h"
#include "StopwatchGameMode.generated.h"

class ACheckpoint;
class ACollapseTrigger;
class AStopwatchPlayerController;

/** What the game is doing. See AStopwatchGameMode for the transitions */
UENUM(BlueprintType)
enum class EStopwatchMode : uint8
{
	/** The title screen. The pawn ignores input */
	Title,

	Playing,

	/** A real pause, over Playing or Collapse */
	Paused,

	/** Fading out on a death, teleporting to the checkpoint, fading back in */
	Respawning,

	/** Playing against the collapse countdown */
	Collapse,

	/** The goal was reached. The pawn ignores input */
	Won
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FOnModeChanged, EStopwatchMode, OldMode, EStopwatchMode, NewMode);

/** The pulse the camera gives during the collapse */
UCLASS()
class UCollapseCameraShake : public UCameraShakeBase
{
	GENERATED_BODY()

public:

	UCollapseCameraShake(const FObjectInitializer& ObjectInitializer);
};

/**
 *  Runs the game's modes, respawns and the collapse, for the single local player
 *
 *   +-------+  any key / Start  +---------+  ACollapseTrigger  +----------+
 *   | Title | ----------------> | Playing | -----------------> | Collapse |
 *   +-------+                   +---------+                    +----------+
 *                                |   ^   |                      |   ^   |
 *                      Esc/Start |   |   | death      Esc/Start |   |   | death, or the
 *                                v   |   v                      v   |   v countdown at 0
 *                         +--------+ +------------+      +--------+ +------------+
 *                         | Paused | | Respawning |      | Paused | | Respawning |
 *                         +--------+ +------------+      +--------+ +------------+
 *                     Esc/Start/Resume:  fade in:    Esc/Start/Resume:  fade in:
 *                     back to Playing    back to     back to Collapse   back to Collapse. The time left is
 *                                        Playing                        kept, unless it ran out: then it restarts
 *
 *   Paused and Respawning are one state each, drawn twice: they go back to the mode they came from
 *   Playing or Collapse --AGoalTrigger--> Won
 *   Paused --Restart--> reload the level --> Playing (the title is skipped)
 *   Won --R / A--> reload the level --> Playing
 *   Paused or Won --Quit--> exit
 *
 *  Every transition is logged and broadcast through OnModeChanged
 */
UCLASS(abstract)
class AStopwatchGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:

	/** Seconds to fade out on a death, and again to fade back in */
	UPROPERTY(EditAnywhere, Category="Respawn", meta=(ClampMin="0.01"))
	float RespawnFadeSeconds = 0.75f;

	/** Sand is topped up to at least this when respawning without a checkpoint */
	UPROPERTY(EditAnywhere, Category="Respawn", meta=(ClampMin="0"))
	float DefaultMinSandOnRespawn = 60.0f;

	/** Below this many seconds left, the countdown turns urgent */
	UPROPERTY(EditAnywhere, Category="Collapse", meta=(ClampMin="0"))
	float CollapseUrgentSeconds = 20.0f;

	/** Seconds between camera pulses during the collapse */
	UPROPERTY(EditAnywhere, Category="Collapse", meta=(ClampMin="0.1"))
	float CollapseShakeInterval = 3.0f;

	/** Seconds between camera pulses once the countdown is urgent */
	UPROPERTY(EditAnywhere, Category="Collapse", meta=(ClampMin="0.1"))
	float CollapseUrgentShakeInterval = 1.0f;

	/** Strength of the camera pulses. 0 turns them off */
	UPROPERTY(EditAnywhere, Category="Collapse", meta=(ClampMin="0"))
	float CollapseShakeScale = 1.0f;

	/** Called on every mode change */
	UPROPERTY(BlueprintAssignable, Category="Mode")
	FOnModeChanged OnModeChanged;

	/** Constructor */
	AStopwatchGameMode();

	UFUNCTION(BlueprintPure, Category="Mode")
	EStopwatchMode GetMode() const { return Mode; }

	/** Playing or Collapse: the player is in control */
	UFUNCTION(BlueprintPure, Category="Mode")
	bool IsInPlay() const { return Mode == EStopwatchMode::Playing || Mode == EStopwatchMode::Collapse; }

	/** Leaves the title screen and starts the playtest log */
	UFUNCTION(BlueprintCallable, Category="Mode")
	void StartPlaying();

	/** Pauses while in play, or resumes */
	UFUNCTION(BlueprintCallable, Category="Mode")
	void TogglePause();

	/** Ends the run and reloads the level, straight into Playing */
	UFUNCTION(BlueprintCallable, Category="Mode")
	void Restart();

	/** Ends the run and exits */
	UFUNCTION(BlueprintCallable, Category="Mode")
	void QuitToDesktop();

	/** Called by the character as it dies. Fades out, respawns at the checkpoint and fades back in */
	void HandlePlayerDeath(AStopwatchCharacter* Character, EDeathCause Cause);

	/** Makes the checkpoint the respawn point if it comes after the current one */
	void ReachCheckpoint(ACheckpoint* Checkpoint);

	/** Shows the section's name and starts its playtest row. Re-entering the current section does nothing */
	void EnterSection(int32 SectionId, const FText& DisplayName);

	/** Starts the countdown, from Playing only */
	void StartCollapse(ACollapseTrigger* Trigger);

	/** Ends the run as completed */
	void Win();

	ACheckpoint* GetCurrentCheckpoint() const { return CurrentCheckpoint.Get(); }
	int32 GetCurrentSectionId() const { return CurrentSectionId; }
	const FText& GetCurrentSectionName() const { return CurrentSectionName; }

	/** True from the collapse trigger until the win, including while paused or respawning */
	bool IsCollapseRunning() const { return ActiveCollapse.IsValid(); }
	float GetCollapseTimeLeft() const { return CollapseTimeLeft; }
	bool IsCollapseUrgent() const { return IsCollapseRunning() && CollapseTimeLeft < CollapseUrgentSeconds; }

	/** For tests and debugging */
	void SetCollapseTimeLeft(float Seconds) { CollapseTimeLeft = Seconds; }

	/** What the death screen says */
	static FText GetDeathCauseText(EDeathCause Cause);

	/** Debug: teleports the player to the checkpoint after the current one */
	void TeleportToNextCheckpoint();

protected:

	/** Starts in Playing when the level was reloaded by Restart */
	virtual void InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage) override;

	virtual void BeginPlay() override;

	/** Runs the collapse countdown, which stops while paused */
	virtual void Tick(float DeltaSeconds) override;

	/** Ends a run that's still going, as a quit */
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

private:

	void SetMode(EStopwatchMode NewMode);

	AStopwatchPlayerController* GetStopwatchController() const;
	AStopwatchCharacter* GetStopwatchCharacter() const;

	/** Halfway through a respawn: teleports, tops up the sand, resets the section and fades in */
	void RespawnPlayer();

	/** Hands control back once the fade in is done */
	void FinishRespawn();

	/** Shows the countdown on the HUD, or hides it once the collapse is over */
	void UpdateCollapseDisplay() const;

	/** Starts the playtest log for a level reloaded by Restart */
	void BeginRestartedRun();

	/** Enters the section and checkpoint triggers the player spawned inside, which never saw them walk in */
	void RegisterStartingTriggers();

	EStopwatchMode Mode = EStopwatchMode::Title;

	/** The mode to go back to after a pause or a respawn */
	EStopwatchMode ResumeMode = EStopwatchMode::Playing;

	TWeakObjectPtr<ACheckpoint> CurrentCheckpoint;
	int32 CurrentSectionId = 0;
	FText CurrentSectionName;

	TWeakObjectPtr<ACollapseTrigger> ActiveCollapse;
	float CollapseTimeLeft = 0.0f;
	float ShakeCountdown = 0.0f;
	EDeathCause LastDeathCause = EDeathCause::Fall;

	bool bRestarted = false;
	FTimerHandle RespawnTimer;
};
