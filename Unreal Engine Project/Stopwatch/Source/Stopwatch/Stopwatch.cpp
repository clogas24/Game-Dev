// Copyright Epic Games, Inc. All Rights Reserved.

#include "Stopwatch.h"
#include "Components/MeshComponent.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Modules/ModuleManager.h"

IMPLEMENT_PRIMARY_GAME_MODULE( FDefaultGameModuleImpl, Stopwatch, "Stopwatch" );

DEFINE_LOG_CATEGORY(LogStopwatch)

UMaterialInstanceDynamic* MakeClockworkMaterial(UMeshComponent* Mesh)
{
	static const TSoftObjectPtr<UMaterialInterface> Clockwork(FSoftObjectPath(TEXT("/Game/Stopwatch/Materials/M_Clockwork.M_Clockwork")));
	UMaterialInterface* Material = Clockwork.LoadSynchronous();

	if (!Material)
	{
		static bool bWarned = false;

		if (!bWarned)
		{
			bWarned = true;
			UE_LOG(LogStopwatch, Warning, TEXT("'%s' is missing. Run Scripts/create_frozen_overlay.py to create it."), *Clockwork.ToString());
		}

		return nullptr;
	}

	// the engine's basic shapes have one material slot
	return Mesh->CreateDynamicMaterialInstance(0, Material);
}
