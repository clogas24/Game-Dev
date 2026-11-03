// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "TimeStateProvider.generated.h"

UINTERFACE(MinimalAPI, meta=(CannotImplementInterfaceInBlueprint))
class UTimeStateProvider : public UInterface
{
	GENERATED_BODY()
};

/**
 *  Implemented by tick-driven actors, so Rewind can run their internal clock backwards instead of fighting their tick
 */
class ITimeStateProvider
{
	GENERATED_BODY()

public:

	/** Returns the actor's internal clock */
	virtual float CaptureTimeState() const = 0;

	/** Sets the internal clock and poses the actor to match */
	virtual void RestoreTimeState(float State) = 0;
};
