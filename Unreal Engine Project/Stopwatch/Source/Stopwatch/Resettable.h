// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "Resettable.generated.h"

UINTERFACE(MinimalAPI, meta=(CannotImplementInterfaceInBlueprint))
class UResettable : public UInterface
{
	GENERATED_BODY()
};

/**
 *  A level piece that goes back to how the level started when the player respawns at a checkpoint of its section
 *  Implementers keep their SectionId as an EditAnywhere property, so it's set per placed actor
 */
class IResettable
{
	GENERATED_BODY()

public:

	/** The section this piece belongs to. 0 means none, so no checkpoint resets it */
	virtual int32 GetSectionId() const = 0;

	/** Puts the piece back in its start state: position, clock, time effects and anything it spawned */
	virtual void ResetToStart() = 0;
};
