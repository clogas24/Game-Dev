// Copyright Epic Games, Inc. All Rights Reserved.


#include "StopwatchPlayerController.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "Engine/Engine.h"
#include "Engine/LocalPlayer.h"
#include "GameFramework/Actor.h"
#include "InputAction.h"
#include "InputKeyEventArgs.h"
#include "InputMappingContext.h"
#include "Blueprint/UserWidget.h"
#include "Camera/PlayerCameraManager.h"
#include "LevelTriggers.h"
#include "PlaytestLogger.h"
#include "Stopwatch.h"
#include "StopwatchCharacter.h"
#include "StopwatchHUDWidget.h"
#include "StopwatchMenuWidget.h"
#include "StopwatchSettingsWidget.h"
#include "TimeAbilityComponent.h"
#include "TimeableComponent.h"
#include "TimeSandComponent.h"
#include "Widgets/Input/SVirtualJoystick.h"

namespace
{
	const TCHAR* SettingsSection = TEXT("Stopwatch.Settings");
	const TCHAR* MouseSensitivityKey = TEXT("MouseSensitivity");
}

void AStopwatchPlayerController::BeginPlay()
{
	Super::BeginPlay();

	if (PlayerCameraManager)
	{
		PlayerCameraManager->ViewPitchMin = MinViewPitch;
		PlayerCameraManager->ViewPitchMax = MaxViewPitch;
	}

	float SavedSensitivity = MouseSensitivity;
	GConfig->GetFloat(SettingsSection, MouseSensitivityKey, SavedSensitivity, GGameUserSettingsIni);
	SetMouseSensitivity(SavedSensitivity);

	if (AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>(); GameMode && IsLocalPlayerController())
	{
		GameMode->OnModeChanged.AddUniqueDynamic(this, &AStopwatchPlayerController::HandleModeChanged);
		ApplyMode(GameMode->GetMode());
	}

	// only spawn touch controls on local player controllers
	if (IsLocalPlayerController() && ShouldUseTouchControls())
	{
		// spawn the mobile controls widget
		MobileControlsWidget = CreateWidget<UUserWidget>(this, MobileControlsWidgetClass);

		if (MobileControlsWidget)
		{
			// add the controls to the player screen
			MobileControlsWidget->AddToPlayerScreen(0);

		} else {

			UE_LOG(LogStopwatch, Error, TEXT("Could not spawn mobile controls widget."));

		}

	}
}

void AStopwatchPlayerController::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);

	AStopwatchCharacter* StopwatchCharacter = Cast<AStopwatchCharacter>(InPawn);

	if (!StopwatchCharacter || !IsLocalPlayerController())
	{
		return;
	}

	// the HUD outlives the pawn, so it only needs creating once
	if (!HUDWidget)
	{
		HUDWidget = CreateWidget<UStopwatchHUDWidget>(this);
		HUDWidget->AddToPlayerScreen(0);
		HUDWidget->SetUsingGamepad(bUsingGamepad);
	}

	HUDWidget->BindTo(StopwatchCharacter->GetTimeSand(), StopwatchCharacter->GetTimeAbility());

	if (const AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>())
	{
		ApplyMode(GameMode->GetMode());
	}
}

void AStopwatchPlayerController::SetupInputComponent()
{
	Super::SetupInputComponent();

	// only add IMCs for local player controllers
	if (IsLocalPlayerController())
	{
		// Add Input Mapping Contexts
		if (UEnhancedInputLocalPlayerSubsystem* Subsystem = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(GetLocalPlayer()))
		{
			for (UInputMappingContext* CurrentContext : DefaultMappingContexts)
			{
				Subsystem->AddMappingContext(CurrentContext, 0);
			}

			// only add these IMCs if we're not using mobile touch input
			if (!ShouldUseTouchControls())
			{
				for (UInputMappingContext* CurrentContext : MobileExcludedMappingContexts)
				{
					Subsystem->AddMappingContext(CurrentContext, 0);
				}
			}

			FreezeAction = NewObject<UInputAction>(this);
			RewindAction = NewObject<UInputAction>(this);
			TimeAbilityMappingContext = NewObject<UInputMappingContext>(this);

			for (const FKey& Key : FreezeKeys)
			{
				TimeAbilityMappingContext->MapKey(FreezeAction, Key);
			}

			for (const FKey& Key : RewindKeys)
			{
				TimeAbilityMappingContext->MapKey(RewindAction, Key);
			}

			// pausing has to work while paused, to resume
			PauseAction = NewObject<UInputAction>(this);
			PauseAction->bTriggerWhenPaused = true;

			for (const FKey& Key : PauseKeys)
			{
				TimeAbilityMappingContext->MapKey(PauseAction, Key);
			}

			// the menu pauses the game, and has to close again while paused
			SettingsMenuAction = NewObject<UInputAction>(this);
			SettingsMenuAction->bTriggerWhenPaused = true;

			for (const FKey& Key : SettingsMenuKeys)
			{
				TimeAbilityMappingContext->MapKey(SettingsMenuAction, Key);
			}

#if !UE_BUILD_SHIPPING
			DebugOverlayAction = NewObject<UInputAction>(this);
			DebugRefillSandAction = NewObject<UInputAction>(this);
			TimeAbilityMappingContext->MapKey(DebugOverlayAction, EKeys::F1);
			TimeAbilityMappingContext->MapKey(DebugRefillSandAction, EKeys::F6);
			DebugToggleViewAction = NewObject<UInputAction>(this);
			TimeAbilityMappingContext->MapKey(DebugToggleViewAction, EKeys::V);
			DebugNextCheckpointAction = NewObject<UInputAction>(this);
			TimeAbilityMappingContext->MapKey(DebugNextCheckpointAction, EKeys::F5);
#endif

			Subsystem->AddMappingContext(TimeAbilityMappingContext, 0);

			if (UEnhancedInputComponent* EnhancedInputComponent = Cast<UEnhancedInputComponent>(InputComponent))
			{
				EnhancedInputComponent->BindAction(FreezeAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::Freeze);
				EnhancedInputComponent->BindAction(RewindAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::Rewind);
				EnhancedInputComponent->BindAction(SettingsMenuAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::ToggleSettingsMenu);
				EnhancedInputComponent->BindAction(PauseAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::HandlePauseInput);

#if !UE_BUILD_SHIPPING
				EnhancedInputComponent->BindAction(DebugOverlayAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::ToggleDebugOverlay);
				EnhancedInputComponent->BindAction(DebugRefillSandAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::DebugRefillSand);
				EnhancedInputComponent->BindAction(DebugToggleViewAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::DebugToggleView);
				EnhancedInputComponent->BindAction(DebugNextCheckpointAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::DebugNextCheckpoint);
#endif
			}
		}
	}
}

bool AStopwatchPlayerController::InputKey(const FInputKeyEventArgs& Params)
{
	AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>();

	// a press that starts or restarts the game is used up, so the same press doesn't also pause or jump
	if (GameMode && Params.Event == IE_Pressed && !Params.Key.IsAxis1D() && !Params.Key.IsAxis2D())
	{
		if (GameMode->GetMode() == EStopwatchMode::Title)
		{
			GameMode->StartPlaying();
			return true;
		}

		if (GameMode->GetMode() == EStopwatchMode::Won && PlayAgainKeys.Contains(Params.Key))
		{
			GameMode->Restart();
			return true;
		}
	}

	// a resting stick still reports small values, which mustn't count as using the gamepad
	const bool bCounts = !Params.Key.IsAnalog() || FMath::Abs(Params.AmountDepressed) > 0.3f;

	if (bCounts && Params.IsGamepad() != bUsingGamepad)
	{
		bUsingGamepad = Params.IsGamepad();

		if (HUDWidget)
		{
			HUDWidget->SetUsingGamepad(bUsingGamepad);
		}
	}

	return Super::InputKey(Params);
}

void AStopwatchPlayerController::Freeze()
{
	if (const AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>(); GameMode && !GameMode->IsInPlay())
	{
		return;
	}

	if (const AStopwatchCharacter* StopwatchCharacter = GetPawn<AStopwatchCharacter>())
	{
		StopwatchCharacter->GetTimeAbility()->TryFreeze();
	}
}

void AStopwatchPlayerController::Rewind()
{
	if (const AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>(); GameMode && !GameMode->IsInPlay())
	{
		return;
	}

	if (const AStopwatchCharacter* StopwatchCharacter = GetPawn<AStopwatchCharacter>())
	{
		StopwatchCharacter->GetTimeAbility()->TryRewind();
	}
}

void AStopwatchPlayerController::SetMouseSensitivity(float NewSensitivity)
{
	MouseSensitivity = FMath::Clamp(NewSensitivity, MinMouseSensitivity, MaxMouseSensitivity);
}

void AStopwatchPlayerController::ToggleSettingsMenu()
{
	// only over play: the title, pause and win screens own the cursor and the pause
	if (const AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>(); GameMode && !GameMode->IsInPlay() && !bSettingsMenuOpen)
	{
		return;
	}

	if (!SettingsWidget)
	{
		SettingsWidget = CreateWidget<UStopwatchSettingsWidget>(this);
		SettingsWidget->AddToPlayerScreen(10);
	}

	bSettingsMenuOpen = !bSettingsMenuOpen;
	SettingsWidget->SetVisibility(bSettingsMenuOpen ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	SetPause(bSettingsMenuOpen);
	SetShowMouseCursor(bSettingsMenuOpen);

	if (bSettingsMenuOpen)
	{
		SettingsWidget->Show(MouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity);

		// nothing focused, so keys still reach the game's input
		FInputModeGameAndUI InputMode;
		InputMode.SetHideCursorDuringCapture(false);
		SetInputMode(InputMode);
	}
	else
	{
		SetInputMode(FInputModeGameOnly());
		GConfig->SetFloat(SettingsSection, MouseSensitivityKey, MouseSensitivity, GGameUserSettingsIni);
		GConfig->Flush(false, GGameUserSettingsIni);
	}
}

bool AStopwatchPlayerController::ShouldUseTouchControls() const
{
	// are we on a mobile platform? Should we force touch?
	return SVirtualJoystick::ShouldDisplayTouchInterface() || bForceTouchControls;
}

void AStopwatchPlayerController::HandlePauseInput()
{
	if (AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>(); GameMode && !bSettingsMenuOpen)
	{
		GameMode->TogglePause();
	}
}

void AStopwatchPlayerController::HandleModeChanged(EStopwatchMode OldMode, EStopwatchMode NewMode)
{
	ApplyMode(NewMode);
}

void AStopwatchPlayerController::ApplyMode(EStopwatchMode NewMode)
{
	if (!IsLocalPlayerController())
	{
		return;
	}

	if (!MenuWidget)
	{
		MenuWidget = CreateWidget<UStopwatchMenuWidget>(this);
		MenuWidget->AddToPlayerScreen(5);
	}

	const UPlaytestLogger* Logger = UPlaytestLogger::Get(this);
	const FString LogFolder = Logger ? Logger->GetFolder() : FString();
	const bool bMenu = NewMode == EStopwatchMode::Title || NewMode == EStopwatchMode::Paused || NewMode == EStopwatchMode::Won;
	const bool bPawnInput = NewMode == EStopwatchMode::Playing || NewMode == EStopwatchMode::Collapse;

	switch (NewMode)
	{
	case EStopwatchMode::Title:
		MenuWidget->ShowTitle();
		break;
	case EStopwatchMode::Paused:
		MenuWidget->ShowPause(LogFolder);
		break;
	case EStopwatchMode::Won:
		MenuWidget->ShowWon(Logger ? Logger->GetTotals() : FPlaytestStats(), LogFolder);
		break;
	default:
		break;
	}

	MenuWidget->SetVisibility(bMenu ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);

	// the title and win screens cover the view, the pause menu sits over it
	if (HUDWidget)
	{
		const bool bHUD = NewMode != EStopwatchMode::Title && NewMode != EStopwatchMode::Won;
		HUDWidget->SetVisibility(bHUD ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
	}

	if (APawn* ControlledPawn = GetPawn())
	{
		if (bPawnInput)
		{
			ControlledPawn->EnableInput(this);
		}
		else
		{
			ControlledPawn->DisableInput(this);
		}
	}

	SetShowMouseCursor(bMenu);

	if (bMenu)
	{
		// keys that the menu doesn't use still reach the game, for any key, pause and play again
		FInputModeGameAndUI InputMode;
		InputMode.SetHideCursorDuringCapture(false);
		SetInputMode(InputMode);
	}
	else
	{
		SetInputMode(FInputModeGameOnly());
	}
}

FText AStopwatchPlayerController::GetActionKeyLabel(const UInputAction* Action, bool bGamepad) const
{
	const UEnhancedInputLocalPlayerSubsystem* Subsystem = GetLocalPlayer() ? ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(GetLocalPlayer()) : nullptr;

	if (!Subsystem || !Action)
	{
		return NSLOCTEXT("Stopwatch", "UnknownKey", "?");
	}

	// the engine's gamepad names are long, like "Gamepad Face Button Bottom"
	static const TMap<FKey, FText> GamepadNames = {
		{ EKeys::Gamepad_FaceButton_Bottom, NSLOCTEXT("Stopwatch", "PadA", "A") },
		{ EKeys::Gamepad_FaceButton_Right, NSLOCTEXT("Stopwatch", "PadB", "B") },
		{ EKeys::Gamepad_FaceButton_Left, NSLOCTEXT("Stopwatch", "PadX", "X") },
		{ EKeys::Gamepad_FaceButton_Top, NSLOCTEXT("Stopwatch", "PadY", "Y") },
		{ EKeys::Gamepad_LeftTrigger, NSLOCTEXT("Stopwatch", "PadLT", "LT") },
		{ EKeys::Gamepad_RightTrigger, NSLOCTEXT("Stopwatch", "PadRT", "RT") },
		{ EKeys::Gamepad_LeftShoulder, NSLOCTEXT("Stopwatch", "PadLB", "LB") },
		{ EKeys::Gamepad_RightShoulder, NSLOCTEXT("Stopwatch", "PadRB", "RB") },
		{ EKeys::Gamepad_Special_Right, NSLOCTEXT("Stopwatch", "PadStart", "Start") },
		{ EKeys::Gamepad_Special_Left, NSLOCTEXT("Stopwatch", "PadBack", "Back") }
	};

	for (const FKey& Key : Subsystem->QueryKeysMappedToAction(Action))
	{
		if (Key.IsGamepadKey() == bGamepad)
		{
			const FText* Short = GamepadNames.Find(Key);
			return Short ? *Short : Key.GetDisplayName(false);
		}
	}

	return NSLOCTEXT("Stopwatch", "UnknownKey", "?");
}

FText AStopwatchPlayerController::FormatPrompt(const FText& Prompt, bool bGamepad) const
{
	const AStopwatchCharacter* StopwatchCharacter = GetPawn<AStopwatchCharacter>();

	FFormatNamedArguments Args;
	Args.Add(TEXT("freeze"), GetActionKeyLabel(FreezeAction, bGamepad));
	Args.Add(TEXT("rewind"), GetActionKeyLabel(RewindAction, bGamepad));
	Args.Add(TEXT("jump"), GetActionKeyLabel(StopwatchCharacter ? StopwatchCharacter->GetJumpAction() : nullptr, bGamepad));
	Args.Add(TEXT("pause"), GetActionKeyLabel(PauseAction, bGamepad));
	return FText::Format(Prompt, Args);
}

#if !UE_BUILD_SHIPPING

void AStopwatchPlayerController::DebugRefillSand()
{
	if (const AStopwatchCharacter* StopwatchCharacter = GetPawn<AStopwatchCharacter>())
	{
		UTimeSandComponent* Sand = StopwatchCharacter->GetTimeSand();
		Sand->SetSand(Sand->MaxSand);
	}
}

void AStopwatchPlayerController::DebugNextCheckpoint()
{
	if (AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>())
	{
		GameMode->TeleportToNextCheckpoint();
	}
}

void AStopwatchPlayerController::DebugToggleView()
{
	if (AStopwatchCharacter* StopwatchCharacter = GetPawn<AStopwatchCharacter>())
	{
		StopwatchCharacter->SetFirstPerson(!StopwatchCharacter->bFirstPerson);
	}
}

void AStopwatchPlayerController::PlayerTick(float DeltaTime)
{
	Super::PlayerTick(DeltaTime);

	const AStopwatchCharacter* StopwatchCharacter = GetPawn<AStopwatchCharacter>();

	if (!bShowDebugOverlay || !StopwatchCharacter || !GEngine)
	{
		return;
	}

	extern ENGINE_API float GAverageFPS;

	FString Text = FString::Printf(TEXT("FPS %.0f   Sand %.0f\n"), GAverageFPS, StopwatchCharacter->GetTimeSand()->GetCurrentSand());

	if (const UTimeableComponent* Target = StopwatchCharacter->GetTimeAbility()->GetAimTarget())
	{
		const TCHAR* TargetState = Target->IsFrozen() ? TEXT("frozen") : Target->IsRewinding() ? TEXT("rewinding") : Target->IsHolding() ? TEXT("holding") : TEXT("normal");

		// a hold that's restored and not frozen still counts down, so both can apply
		Text += FString::Printf(TEXT("Target %s: %s   hold left %.1fs   buffer %.1fs   can rewind %s"),
			*GetNameSafe(Target->GetOwner()), TargetState, Target->GetHoldTimeLeft(), Target->GetBufferSeconds(), Target->CanBeRewound() ? TEXT("yes") : TEXT("no"));
	}
	else
	{
		Text += TEXT("No target");
	}

	constexpr uint64 OverlayKey = 0x5354574348;
	GEngine->AddOnScreenDebugMessage(OverlayKey, 0.0f, FColor::Yellow, Text);
}

#endif
