// Copyright Epic Games, Inc. All Rights Reserved.

#include "StopwatchGameMode.h"
#include "Camera/PlayerCameraManager.h"
#include "Components/CapsuleComponent.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerStart.h"
#include "Kismet/GameplayStatics.h"
#include "Kismet/KismetSystemLibrary.h"
#include "LevelTriggers.h"
#include "PlaytestLogger.h"
#include "Resettable.h"
#include "Shakes/WaveOscillatorCameraShakePattern.h"
#include "Stopwatch.h"
#include "StopwatchHUDWidget.h"
#include "StopwatchPlayerController.h"
#include "TimerManager.h"
#include "TimeSandComponent.h"

namespace
{
	const TCHAR* RestartOption = TEXT("Restart");
}

UCollapseCameraShake::UCollapseCameraShake(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{
	// a short rumble: mostly a nod and a roll, so the aim stays usable
	UWaveOscillatorCameraShakePattern* Pattern = CreateDefaultSubobject<UWaveOscillatorCameraShakePattern>(TEXT("Pattern"));
	Pattern->Duration = 0.5f;
	Pattern->BlendInTime = 0.05f;
	Pattern->BlendOutTime = 0.3f;
	Pattern->Pitch.Amplitude = 0.6f;
	Pattern->Pitch.Frequency = 9.0f;
	Pattern->Roll.Amplitude = 0.8f;
	Pattern->Roll.Frequency = 7.0f;
	Pattern->Z.Amplitude = 3.0f;
	Pattern->Z.Frequency = 11.0f;
	SetRootShakePattern(Pattern);
}

AStopwatchGameMode::AStopwatchGameMode()
{
	PrimaryActorTick.bCanEverTick = true;
	CurrentSectionName = NSLOCTEXT("Stopwatch", "StartSection", "Start");
}

void AStopwatchGameMode::InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage)
{
	Super::InitGame(MapName, Options, ErrorMessage);

	bRestarted = UGameplayStatics::HasOption(Options, RestartOption);
	Mode = bRestarted ? EStopwatchMode::Playing : EStopwatchMode::Title;
	UE_LOG(LogStopwatch, Log, TEXT("Game mode starts in %s"), *UEnum::GetValueAsString(Mode));
}

void AStopwatchGameMode::BeginPlay()
{
	Super::BeginPlay();

	if (bRestarted)
	{
		GetWorldTimerManager().SetTimerForNextTick(this, &AStopwatchGameMode::BeginRestartedRun);
	}
}

void AStopwatchGameMode::RegisterStartingTriggers()
{
	AStopwatchCharacter* Character = GetStopwatchCharacter();

	if (!Character)
	{
		return;
	}

	TArray<AActor*> Triggers;
	Character->GetOverlappingActors(Triggers, APlayerTrigger::StaticClass());

	for (AActor* Trigger : Triggers)
	{
		CastChecked<APlayerTrigger>(Trigger)->NotifyPlayerInside(Character);
	}
}

void AStopwatchGameMode::BeginRestartedRun()
{
	RegisterStartingTriggers();

	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->BeginRun(CurrentSectionId, CurrentSectionName.ToString());
	}
}

void AStopwatchGameMode::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->SetPaused(false);
		Logger->EndRun(false, TEXT("quit"));
	}

	Super::EndPlay(EndPlayReason);
}

void AStopwatchGameMode::SetMode(EStopwatchMode NewMode)
{
	if (NewMode == Mode)
	{
		return;
	}

	const EStopwatchMode OldMode = Mode;
	Mode = NewMode;
	UE_LOG(LogStopwatch, Log, TEXT("Game mode: %s -> %s"), *UEnum::GetValueAsString(OldMode), *UEnum::GetValueAsString(NewMode));
	OnModeChanged.Broadcast(OldMode, NewMode);
}

AStopwatchPlayerController* AStopwatchGameMode::GetStopwatchController() const
{
	return GetWorld()->GetFirstPlayerController<AStopwatchPlayerController>();
}

AStopwatchCharacter* AStopwatchGameMode::GetStopwatchCharacter() const
{
	const APlayerController* Controller = GetWorld()->GetFirstPlayerController();
	return Controller ? Controller->GetPawn<AStopwatchCharacter>() : nullptr;
}

void AStopwatchGameMode::StartPlaying()
{
	if (Mode != EStopwatchMode::Title)
	{
		return;
	}

	// so the log starts in the section the player spawned in
	RegisterStartingTriggers();

	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->BeginRun(CurrentSectionId, CurrentSectionName.ToString());
	}

	SetMode(EStopwatchMode::Playing);
}

void AStopwatchGameMode::TogglePause()
{
	AStopwatchPlayerController* Controller = GetStopwatchController();
	UPlaytestLogger* Logger = UPlaytestLogger::Get(this);

	if (IsInPlay())
	{
		ResumeMode = Mode;

		if (Controller)
		{
			Controller->SetPause(true);
		}

		if (Logger)
		{
			Logger->SetPaused(true);
		}

		SetMode(EStopwatchMode::Paused);
	}
	else if (Mode == EStopwatchMode::Paused)
	{
		if (Controller)
		{
			Controller->SetPause(false);
		}

		if (Logger)
		{
			Logger->SetPaused(false);
		}

		SetMode(ResumeMode);
	}
}

void AStopwatchGameMode::Restart()
{
	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->SetPaused(false);
		Logger->EndRun(false, TEXT("restart"));
	}

	UGameplayStatics::OpenLevel(this, FName(*UGameplayStatics::GetCurrentLevelName(this)), true, RestartOption);
}

void AStopwatchGameMode::QuitToDesktop()
{
	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->SetPaused(false);
		Logger->EndRun(false, TEXT("quit"));
	}

	UKismetSystemLibrary::QuitGame(this, GetWorld()->GetFirstPlayerController(), EQuitPreference::Quit, false);
}

FText AStopwatchGameMode::GetDeathCauseText(EDeathCause Cause)
{
	switch (Cause)
	{
	case EDeathCause::Crushed:
		return NSLOCTEXT("Stopwatch", "DeathCrushed", "Crushed");
	case EDeathCause::Blade:
		return NSLOCTEXT("Stopwatch", "DeathBlade", "Blade");
	case EDeathCause::Collapse:
		return NSLOCTEXT("Stopwatch", "DeathCollapse", "The tower collapsed");
	default:
		return NSLOCTEXT("Stopwatch", "DeathFall", "Fell");
	}
}

void AStopwatchGameMode::HandlePlayerDeath(AStopwatchCharacter* Character, EDeathCause Cause)
{
	if (!IsInPlay())
	{
		return;
	}

	LastDeathCause = Cause;
	ResumeMode = Mode;
	SetMode(EStopwatchMode::Respawning);

	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->RecordDeath(Cause);
	}

	if (AStopwatchPlayerController* Controller = GetStopwatchController())
	{
		Controller->PlayerCameraManager->StartCameraFade(0.0f, 1.0f, RespawnFadeSeconds, FLinearColor::Black, false, true);

		if (UStopwatchHUDWidget* HUD = Controller->GetHUDWidget())
		{
			HUD->ShowDeathCause(GetDeathCauseText(Cause));
		}
	}

	GetWorldTimerManager().SetTimer(RespawnTimer, this, &AStopwatchGameMode::RespawnPlayer, RespawnFadeSeconds);
}

void AStopwatchGameMode::RespawnPlayer()
{
	AStopwatchCharacter* Character = GetStopwatchCharacter();
	AStopwatchPlayerController* Controller = GetStopwatchController();

	if (!Character)
	{
		FinishRespawn();
		return;
	}

	const ACheckpoint* Checkpoint = CurrentCheckpoint.Get();
	FVector Feet = FVector::ZeroVector;
	FRotator Facing = FRotator::ZeroRotator;

	if (Checkpoint)
	{
		Feet = Checkpoint->GetActorLocation();
		Facing = FRotator(0.0f, Checkpoint->GetActorRotation().Yaw, 0.0f);
	}
	else if (TActorIterator<APlayerStart> PlayerStart(GetWorld()); PlayerStart)
	{
		// a PlayerStart's location is the capsule's center
		Feet = PlayerStart->GetActorLocation() - FVector(0.0f, 0.0f, Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight());
		Facing = FRotator(0.0f, PlayerStart->GetActorRotation().Yaw, 0.0f);
	}

	Character->TeleportTo(Feet + FVector(0.0f, 0.0f, Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight()), Facing, false, true);
	Character->GetCharacterMovement()->StopMovementImmediately();
	Character->Revive();

	// the section's pieces go back to how the level started, so a retry starts clean
	// after the teleport, so a pickup the player died on doesn't get taken again as it comes back
	const int32 SectionId = Checkpoint ? Checkpoint->SectionId : 0;

	if (SectionId != 0)
	{
		for (TActorIterator<AActor> It(GetWorld()); It; ++It)
		{
			if (IResettable* Resettable = Cast<IResettable>(*It); Resettable && Resettable->GetSectionId() == SectionId)
			{
				Resettable->ResetToStart();
			}
			}
	}

	UTimeSandComponent* Sand = Character->GetTimeSand();
	Sand->SetSand(FMath::Max(Sand->GetCurrentSand(), Checkpoint ? Checkpoint->MinSandOnRespawn : DefaultMinSandOnRespawn));

	// a fall or a hazard keeps the time that was left. Running out of time has to give it back, or the respawn would die again at once
	if (ResumeMode == EStopwatchMode::Collapse && LastDeathCause == EDeathCause::Collapse)
	{
		if (const ACollapseTrigger* Collapse = ActiveCollapse.Get())
		{
			CollapseTimeLeft = Collapse->CollapseSeconds;
			ShakeCountdown = 0.0f;
			UpdateCollapseDisplay();
		}
	}

	if (Controller)
	{
		Controller->SetControlRotation(Facing);
		Controller->PlayerCameraManager->StopAllCameraShakes(true);
		Controller->PlayerCameraManager->StartCameraFade(1.0f, 0.0f, RespawnFadeSeconds, FLinearColor::Black);

		if (UStopwatchHUDWidget* HUD = Controller->GetHUDWidget())
		{
			HUD->HideDeathCause();
		}
	}

	UE_LOG(LogStopwatch, Log, TEXT("Respawned at %s, section %d reset"), Checkpoint ? *Checkpoint->GetName() : TEXT("the PlayerStart"), SectionId);
	GetWorldTimerManager().SetTimer(RespawnTimer, this, &AStopwatchGameMode::FinishRespawn, RespawnFadeSeconds);
}

void AStopwatchGameMode::FinishRespawn()
{
	if (Mode == EStopwatchMode::Respawning)
	{
		SetMode(ResumeMode);
	}
}

void AStopwatchGameMode::ReachCheckpoint(ACheckpoint* Checkpoint)
{
	if (!Checkpoint || Checkpoint == CurrentCheckpoint.Get() || !Checkpoint->IsAfter(CurrentCheckpoint.Get()))
	{
		return;
	}

	CurrentCheckpoint = Checkpoint;
	Checkpoint->SetLit(true);
	UE_LOG(LogStopwatch, Log, TEXT("Checkpoint %s: section %d, order %d"), *Checkpoint->GetName(), Checkpoint->SectionId, Checkpoint->OrderInSection);
}

void AStopwatchGameMode::EnterSection(int32 SectionId, const FText& DisplayName)
{
	if (SectionId == CurrentSectionId)
	{
		return;
	}

	CurrentSectionId = SectionId;
	CurrentSectionName = DisplayName;
	UE_LOG(LogStopwatch, Log, TEXT("Section %d: %s"), SectionId, *DisplayName.ToString());

	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->EnterSection(SectionId, DisplayName.ToString());
	}

	const AStopwatchPlayerController* Controller = GetStopwatchController();

	if (UStopwatchHUDWidget* HUD = Controller ? Controller->GetHUDWidget() : nullptr)
	{
		HUD->ShowSectionBanner(DisplayName);
	}
}

void AStopwatchGameMode::StartCollapse(ACollapseTrigger* Trigger)
{
	if (Mode != EStopwatchMode::Playing || !Trigger)
	{
		return;
	}

	ActiveCollapse = Trigger;
	CollapseTimeLeft = Trigger->CollapseSeconds;
	ShakeCountdown = 0.0f;
	UpdateCollapseDisplay();
	SetMode(EStopwatchMode::Collapse);
}

void AStopwatchGameMode::UpdateCollapseDisplay() const
{
	const AStopwatchPlayerController* Controller = GetStopwatchController();
	UStopwatchHUDWidget* HUD = Controller ? Controller->GetHUDWidget() : nullptr;

	if (!HUD)
	{
		return;
	}

	if (IsCollapseRunning())
	{
		HUD->ShowCollapse(CollapseTimeLeft, IsCollapseUrgent());
	}
	else
	{
		HUD->HideCollapse();
	}
}

void AStopwatchGameMode::Win()
{
	if (!IsInPlay())
	{
		return;
	}

	ActiveCollapse = nullptr;
	UpdateCollapseDisplay();

	if (UPlaytestLogger* Logger = UPlaytestLogger::Get(this))
	{
		Logger->EndRun(true, TEXT("win"));
	}

	SetMode(EStopwatchMode::Won);
}

void AStopwatchGameMode::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	if (Mode != EStopwatchMode::Collapse)
	{
		return;
	}

	CollapseTimeLeft = FMath::Max(CollapseTimeLeft - DeltaSeconds, 0.0f);
	ShakeCountdown -= DeltaSeconds;
	UpdateCollapseDisplay();

	const AStopwatchPlayerController* Controller = GetStopwatchController();

	if (ShakeCountdown <= 0.0f && Controller && CollapseShakeScale > 0.0f)
	{
		Controller->PlayerCameraManager->StartCameraShake(UCollapseCameraShake::StaticClass(), CollapseShakeScale * (IsCollapseUrgent() ? 1.5f : 1.0f));
		ShakeCountdown = IsCollapseUrgent() ? CollapseUrgentShakeInterval : CollapseShakeInterval;
	}

	if (CollapseTimeLeft <= 0.0f)
	{
		if (AStopwatchCharacter* Character = GetStopwatchCharacter())
		{
			Character->KillPlayer(EDeathCause::Collapse);
		}
	}
}

void AStopwatchGameMode::TeleportToNextCheckpoint()
{
	AStopwatchCharacter* Character = GetStopwatchCharacter();
	ACheckpoint* Next = nullptr;

	for (TActorIterator<ACheckpoint> It(GetWorld()); It; ++It)
	{
		if (It->IsAfter(CurrentCheckpoint.Get()) && (!Next || Next->IsAfter(*It)))
		{
			Next = *It;
		}
	}

	if (!Character || !Next || !IsInPlay())
	{
		return;
	}

	const FRotator Facing(0.0f, Next->GetActorRotation().Yaw, 0.0f);
	Character->TeleportTo(Next->GetActorLocation() + FVector(0.0f, 0.0f, Character->GetCapsuleComponent()->GetScaledCapsuleHalfHeight()), Facing, false, true);
	Character->GetCharacterMovement()->StopMovementImmediately();

	if (AController* Controller = Character->GetController())
	{
		Controller->SetControlRotation(Facing);
	}

	ReachCheckpoint(Next);
}
