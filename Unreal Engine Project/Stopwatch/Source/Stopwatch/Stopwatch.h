// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"

class UMaterialInstanceDynamic;
class UMeshComponent;

/** Main log category used across the project */
DECLARE_LOG_CATEGORY_EXTERN(LogStopwatch, Log, All);

/**
 *  Gives the mesh an instance of M_Clockwork, the brass look of everything the player can interact with
 *  Its parameters: BaseColor, RimColor and RimAmount for a pulsing glow, and Flash for a red warning
 *  Returns null if the material is missing. Run Scripts/create_frozen_overlay.py to create it
 */
UMaterialInstanceDynamic* MakeClockworkMaterial(UMeshComponent* Mesh);
