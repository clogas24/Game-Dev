// Copyright Epic Games, Inc. All Rights Reserved.

#include "RollTrigger.h"
#include "Components/BoxComponent.h"
#include "DropChute.h"
#include "PhysicsProp.h"
#include "StopwatchCharacter.h"

ARollTrigger::ARollTrigger()
{
	Box = CreateDefaultSubobject<UBoxComponent>(TEXT("Box"));
	Box->SetCollisionProfileName(TEXT("Trigger"));
	Box->OnComponentBeginOverlap.AddDynamic(this, &ARollTrigger::OnBoxBeginOverlap);
	RootComponent = Box;
}

void ARollTrigger::OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	if (!Cast<AStopwatchCharacter>(OtherActor))
	{
		return;
	}

	if (TargetProp)
	{
		TargetProp->Wake(PushVelocity);
	}

	if (TargetChute)
	{
		TargetChute->ReleaseProp();
	}
}
