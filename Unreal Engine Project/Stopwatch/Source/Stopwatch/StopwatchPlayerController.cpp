// Copyright Epic Games, Inc. All Rights Reserved.


#include "StopwatchPlayerController.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "Engine/Engine.h"
#include "Engine/LocalPlayer.h"
#include "GameFramework/Actor.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "Blueprint/UserWidget.h"
#include "Camera/PlayerCameraManager.h"
#include "Stopwatch.h"
#include "StopwatchCharacter.h"
#include "StopwatchHUDWidget.h"
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
	}

	HUDWidget->BindTo(StopwatchCharacter->GetTimeSand(), StopwatchCharacter->GetTimeAbility());
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
#endif

			Subsystem->AddMappingContext(TimeAbilityMappingContext, 0);

			if (UEnhancedInputComponent* EnhancedInputComponent = Cast<UEnhancedInputComponent>(InputComponent))
			{
				EnhancedInputComponent->BindAction(FreezeAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::Freeze);
				EnhancedInputComponent->BindAction(RewindAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::Rewind);
				EnhancedInputComponent->BindAction(SettingsMenuAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::ToggleSettingsMenu);

#if !UE_BUILD_SHIPPING
				EnhancedInputComponent->BindAction(DebugOverlayAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::ToggleDebugOverlay);
				EnhancedInputComponent->BindAction(DebugRefillSandAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::DebugRefillSand);
				EnhancedInputComponent->BindAction(DebugToggleViewAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::DebugToggleView);
#endif
			}
		}
	}
}

void AStopwatchPlayerController::Freeze()
{
	if (const AStopwatchCharacter* StopwatchCharacter = GetPawn<AStopwatchCharacter>())
	{
		StopwatchCharacter->GetTimeAbility()->TryFreeze();
	}
}

void AStopwatchPlayerController::Rewind()
{
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

#if !UE_BUILD_SHIPPING

void AStopwatchPlayerController::DebugRefillSand()
{
	if (const AStopwatchCharacter* StopwatchCharacter = GetPawn<AStopwatchCharacter>())
	{
		UTimeSandComponent* Sand = StopwatchCharacter->GetTimeSand();
		Sand->SetSand(Sand->MaxSand);
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
