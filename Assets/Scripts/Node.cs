using UnityEngine;
using System.Collections;

public class Node : GameScript.Combatable
{

    public bool isCurrentlyInCapture = false;

    public bool isCurrentlyUpgrading = false;

    public int APUpgradeLevel = 0;

    public int currentCaptureIterator;

    public int currentUpgradeIterator;

    public int nodeCellElement;

    public int scaleFactor;

    public int upgradeCurve = 3;
    
    private void Awake()
    {
        currentTier = 1;
    }

    public int GetUpgradeCost()
    {
        return currentTier * upgradeCurve;
    }

    public int GetTotalAPValue()
    {
        return currentTier + APUpgradeLevel;
    }
}