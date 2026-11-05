// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "Components/PrimitiveComponent.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "StopwatchGameMode.h"

/** Shared by the tests that play a saved level in -game */
namespace StopwatchTests
{
	template <typename T, typename TPredicate>
	T* FindActor(UWorld* World, TPredicate Predicate)
	{
		for (TActorIterator<T> It(World); It; ++It)
		{
			if (Predicate(*It))
			{
				return *It;
			}
		}

		return nullptr;
	}

	template <typename T>
	int32 CountActors(UWorld* World)
	{
		int32 Count = 0;

		for (TActorIterator<T> It(World); It; ++It)
		{
			++Count;
		}

		return Count;
	}

	/** The running game's world, if it's playing the named level */
	inline UWorld* FindGameWorld(const TCHAR* LevelName)
	{
		for (const FWorldContext& Context : GEngine->GetWorldContexts())
		{
			if (Context.WorldType == EWorldType::Game && Context.World() && Context.World()->GetName() == LevelName)
			{
				return Context.World();
			}
		}

		return nullptr;
	}

	/** Reads a parameter of the clockwork material Timeable gave the mesh, or -1 if it has none */
	inline float GetMaterialScalar(const UPrimitiveComponent* Mesh, FName Name)
	{
		UMaterialInstanceDynamic* Material = Cast<UMaterialInstanceDynamic>(Mesh->GetMaterial(0));
		return Material ? Material->K2_GetScalarParameterValue(Name) : -1.0f;
	}

	/** Leaves the title screen, as a key press would. Returns true if the game was on it */
	inline bool EnsurePlaying(UWorld* World)
	{
		AStopwatchGameMode* GameMode = World->GetAuthGameMode<AStopwatchGameMode>();
		const bool bOnTitle = GameMode && GameMode->GetMode() == EStopwatchMode::Title;

		if (bOnTitle)
		{
			GameMode->StartPlaying();
		}

		return bOnTitle;
	}
}

#endif
