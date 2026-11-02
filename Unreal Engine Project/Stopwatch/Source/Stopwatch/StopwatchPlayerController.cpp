// Copyright Epic Games, Inc. All Rights Reserved.


#include "StopwatchPlayerController.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "Engine/LocalPlayer.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "Blueprint/UserWidget.h"
#include "Stopwatch.h"
#include "StopwatchCharacter.h"
#include "StopwatchHUDWidget.h"
#include "TimeAbilityComponent.h"
#include "Widgets/Input/SVirtualJoystick.h"

void AStopwatchPlayerController::BeginPlay()
{
	Super::BeginPlay();

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

			Subsystem->AddMappingContext(TimeAbilityMappingContext, 0);

			if (UEnhancedInputComponent* EnhancedInputComponent = Cast<UEnhancedInputComponent>(InputComponent))
			{
				EnhancedInputComponent->BindAction(FreezeAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::Freeze);
				EnhancedInputComponent->BindAction(RewindAction, ETriggerEvent::Started, this, &AStopwatchPlayerController::Rewind);
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
	UE_LOG(LogStopwatch, Log, TEXT("Rewind not implemented yet"));
}

bool AStopwatchPlayerController::ShouldUseTouchControls() const
{
	// are we on a mobile platform? Should we force touch?
	return SVirtualJoystick::ShouldDisplayTouchInterface() || bForceTouchControls;
}
