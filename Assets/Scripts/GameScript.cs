using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameScript : MonoBehaviour {

    #region Globals

    #region Arrays

    int[] currentPathElements;
    int[] previousPathElements;

    HexCell[] hexCells;

    //Loops counter clockwise
    Vector2[] neighborDirection = new Vector2[] { new Vector2( 1, 0 ), new Vector2( 0,  1),
                                                  new Vector2( -1, 1 ), new Vector2(-1,  0),
                                                  new Vector2(0, -1), new Vector2( 1, -1) };

    #endregion

    #region Bools

    bool capturingNode;

    bool combatMove;

    bool counterAttackCheck;

    bool currentSpawnIteratorHasChanged;

    bool currentUnitHasSpawned = false;

    bool defenderSurvivedBattle;

    bool doCounterAttack;

    bool initialDirectionHasBeenSet = false;

    bool isInitialRotation;

    bool moveCurrentUnit;

    bool targetIsPartOfPath;

    bool useAutoSequenceForBattleChain = true;

    #endregion

    #region Colors

    public Color currentPlayerColor;
    public Color playerOneColor;
    public Color playerTwoColor;

    #endregion

    #region Enumerators

    enum HexNeighbor { RIGHT, UP_RIGHT, UP_LEFT, LEFT, DOWN_LEFT, DOWN_RIGHT };

    HexNeighbor neighborAngle;

    enum CombatClash { UnitAttacksNode, UnitAttacksUnit, NodeAttacksUnit };

    CombatClash clash;

    #endregion

    #region Floats

    float movementTValue;

    public float globalMoveSpeed = 1;
    public float unitScalePercentage;

    #endregion

    #region Game Object

    GameObject currentActionNode;

    GameObject currentNodeBeingCaptured;

    GameObject currentSelection;

    GameObject currentTarget;

    GameObject currentUnitToSpawn;

    public GameObject nodePrefab;
    public GameObject unitPrefab;

    #endregion

    #region Int

    public int boardSizeX;
    public int boardSizeY;

    int currentSpawnLevelIterator;

    int currentTargetElement;

    int initialUnitDirection;

    public int globalMaxTier;

    int movementPathIterator;

    #endregion

    #region Layers

    int playerOneLayer = 8;
    int playerTwoLayer = 9;

    #endregion

    #region Lists

    List<int> UnitRotateCellNeighbors;

    List<int> UnitSpawnCellNeighbors;

    #endregion

    #region Materials

    public Material playerOneMaterial;
    public Material playerTwoMaterial;

    #endregion

    #region Node

    Node attackNode;
    Node defendingNode;

    #endregion

    #region Players

    Player attackingPlayer;
    Player defendingPlayer;

    Player currentPlayer;
    Player otherPlayer;

    Player PlayerOne;
    Player PlayerTwo;

    #endregion

    #region Types

    Type currentSelectionType;
    Type currentTargetType;

    #endregion

    #region Vector3

    Vector3 baseUnitScale;

    Vector3 unitMovementLerpStart;

    #endregion

    #region BoardGeneration

    Mesh hexBoardMesh;
    MeshCollider hexBoardCollider;

    #region MeshLists

    List<Color>   boardColors;
    List<int>     boardTriangles;
    List<Vector2> boardUVs;
    List<Vector3> boardVertices;

    #endregion MeshLists

    #endregion BoardGeneration

    #region Misc.

    public BoardData boardData;

    #endregion

    #region UI

    [Serializable]
    public class UIElements
    {        
        [Serializable]
        public class NodePanelElements
        {
            public GameObject nodePanel;
            public Button spawnUnit;
            public Text nodeTier;
            public Text tierCost;
            public Text upgradeStatus;
        }

        [Serializable]
        public class UnitPanelElements
        {
            public GameObject unitPanel;
            public Button rotateUnit;
            public Text unitTier;
            public Text unitHealth;
        }

        [Serializable]
        public class SpawnTierPanel
        {
            public GameObject panel;

            public Text spawnLevel;
        }

        public GameObject playerOneColorPanel;
        public GameObject playerTwoColorPanel;

        public NodePanelElements NodePanel;

        public UnitPanelElements UnitPanel;

        public SpawnTierPanel SpawnPanel;

        public GameObject playerColorOutlinePanel;

        public GameObject textAP; 
    }

    public UIElements UI;   

    Toggle[] playerOneToggles;
    Toggle[] playerTwoToggles;

    #endregion

    #region Unit

    Unit attackingUnit;
    Unit defendingUnit;

    Unit newAttacker;
    Unit newDefender;
    
    //This may change at some point...
    Combatable.Flank currentlyTargetedFlank;

    #endregion

    #endregion Globals

    #region State Machines

    enum ObjectHandler { Idle, InitiateUnitRotate, InitiateUnitSpawn, RotateUnit, SpawnUnit, ExitRotateUnit, ExitUnitSpawn,
                         PathUnit, MoveUnit, InitializeCombat, Combat, ConfirmCounterAttack, UpgradeNode }
    enum PlayerState
    {
        PLAYER_ONE_INITIALIZE, PLAYER_TWO_INITIALIZE, PLAYER_ONE_ACTIVE,
        PLAYER_TWO_ACTIVE, PLAYER_ONE_EXIT, PLAYER_TWO_EXIT
    }

    ObjectHandler currentObjectState;
    PlayerState currentPlayerState;

    Dictionary<ObjectHandler, Action> osm = new Dictionary<ObjectHandler, Action>();

    Dictionary<PlayerState  , Action> fsm = new Dictionary<PlayerState  , Action>();

    #region State Functions

    #region Object State

    void Idle()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            EndTurn();

        SelectionRaycast();

        if (currentSelection == null)
            return;

        //This check allows for either player to select any piece or node for stat checking, while removing the ability for the current player to take action on the other player's stuff
        if (currentSelection.layer == currentPlayer.layer)
        {
            if (currentSelection.tag == "Node" && Input.GetKeyDown(KeyCode.A) && !currentSelection.GetComponent<Node>().isCurrentlyUpgrading && !currentSelection.GetComponent<Node>().isCurrentlyInCapture)
                CallUpgradeNode();

            if (Input.GetKeyDown(KeyCode.S) && !currentSelection.GetComponent<Node>().isCurrentlyUpgrading && !currentSelection.GetComponent<Node>().isCurrentlyInCapture)
                currentObjectState = ObjectHandler.InitiateUnitSpawn;

            if (Input.GetKeyDown(KeyCode.R))
            {
                if(currentSelection.tag == "Unit")
                {
                    currentObjectState = ObjectHandler.InitiateUnitRotate;
                }
            }
        }
    }

    void InitiateUnitRotate()
    {
        UnitRotateCellNeighbors = GetCellNeighbors(hexCells[ReturnHexCellElement(currentSelection.transform.position)]);

        if (!initialDirectionHasBeenSet)
        {
            initialUnitDirection = currentSelection.GetComponent<Unit>().unitDirection;
        }

        currentObjectState = ObjectHandler.RotateUnit;
    }

    void InitiateUnitSpawn()
    {
        initialDirectionHasBeenSet = false; //Set this to false upon a new unit spawn in order to deduct AP upon finishing initial spawn rotation

        UnitSpawnCellNeighbors = GetCellNeighborsForPathing(hexCells[ReturnHexCellElement(currentSelection.transform.position)]);

        for (int i = 0; i < UnitSpawnCellNeighbors.Count; i++)
            ColorSingleCell(UnitSpawnCellNeighbors[i], currentPlayerColor);
        
        UI.SpawnPanel.spawnLevel.text = currentSelection.GetComponent<Node>().currentTier.ToString();        
        UI.SpawnPanel.panel.GetComponent<Image>().color = currentPlayerColor;

        currentSpawnLevelIterator = currentSelection.GetComponent<Node>().currentTier;

        if (currentPlayer.currentAP < currentSpawnLevelIterator)
            UI.SpawnPanel.spawnLevel.GetComponent<Text>().color = Color.gray;
        else
            UI.SpawnPanel.spawnLevel.GetComponent<Text>().color = Color.black;

        UI.SpawnPanel.panel.SetActive(true);

        currentObjectState = ObjectHandler.SpawnUnit;
    }

    void RotateUnit()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (UnitRotateCellNeighbors.Contains(ReturnHexCellElement(hit.point)))
            {
                CalculateRotationAngleFromVector3(hit.point);

                if (Input.GetMouseButtonDown(0))
                {
                    if (!initialDirectionHasBeenSet)
                    {                        
                        //Casting this to an int saves me from having to refactor code to get the enum in the Unit script
                        currentUnitToSpawn.GetComponent<Unit>().unitDirection = (int)neighborAngle;

                        initialDirectionHasBeenSet = true;

                        currentPlayer.currentAP -= currentUnitToSpawn.GetComponent<Unit>().currentTier = currentSpawnLevelIterator;
                    }

                    else if(currentSelection.GetComponent<Unit>().unitDirection != (int)neighborAngle)
                    {
                        currentSelection.GetComponent<Unit>().unitDirection = (int)neighborAngle;

                        //This line is used to make unit rotation have an AP cost
                        //currentPlayer.currentAP -= currentSelection.GetComponent<Unit>().currentTier = currentSpawnLevelIterator;
                    }

                    UpdateUnitPanelValues();

                    currentObjectState = ObjectHandler.ExitRotateUnit;
                }

                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (!initialDirectionHasBeenSet)
                    {
                        hexCells[ReturnHexCellElement(currentUnitToSpawn.transform.position)].hasUnit = false;

                        Destroy(currentUnitToSpawn);

                        currentSelection = currentActionNode;

                        currentObjectState = ObjectHandler.Idle;
                    }

                    else
                    {
                        neighborAngle = (HexNeighbor)initialUnitDirection;
                        SetRotationFromNeighborAngle();
                        currentObjectState = ObjectHandler.Idle;
                    }
                }
            }
        }
    }

    void SpawnUnit()
    {
        SpawnTierPanelUpdate();

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if(hit.collider.tag == "Board" || hit.collider.gameObject == currentUnitToSpawn)
            {
                if( UnitSpawnCellNeighbors.Contains(ReturnHexCellElement(hit.point)))
                {                       
                    if (currentUnitHasSpawned == false)
                    {
                        currentUnitToSpawn = Instantiate(unitPrefab, hexCells[ReturnHexCellElement(hit.point)].cellPosition, Quaternion.identity) as GameObject;
                        currentUnitToSpawn.transform.localScale = Vector3.one + (Vector3.one * unitScalePercentage * currentSpawnLevelIterator);                        
                        currentUnitToSpawn.GetComponent<Unit>().SetUVsForUnitTopFromUnitTier(currentSpawnLevelIterator);

                        ColorSpawnFromAPCheck();
                        currentUnitHasSpawned = true;
                    }

                    if (currentSpawnIteratorHasChanged)
                    {
                        currentUnitToSpawn.transform.localScale = Vector3.one + (Vector3.one * unitScalePercentage * currentSpawnLevelIterator);
                        currentUnitToSpawn.GetComponent<Unit>().SetUVsForUnitTopFromUnitTier(currentSpawnLevelIterator);
                        ColorSpawnFromAPCheck();
                    }

                    if (currentUnitHasSpawned)
                    {
                        currentUnitToSpawn.transform.position = hexCells[ReturnHexCellElement(hit.point)].cellPosition + ((Vector3.up / currentUnitToSpawn.GetComponent<Unit>().hexSizeDivisor) * currentUnitToSpawn.transform.localScale.y);                        
                    }

                    if (Input.GetMouseButtonDown(0) && currentPlayer.currentAP >= currentSpawnLevelIterator)
                    {
                        hexCells[ReturnHexCellElement(currentUnitToSpawn.transform.position)].hasUnit = true;
                        hexCells[ReturnHexCellElement(currentUnitToSpawn.transform.position)].cellUnit = currentUnitToSpawn;
                        currentPlayer.AddToUnitCollection(currentUnitToSpawn);                        
                        currentSelection = currentUnitToSpawn;

                        currentObjectState = ObjectHandler.ExitUnitSpawn;
                    }
                }
            }            
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentUnitHasSpawned)           
                Destroy(currentUnitToSpawn);

            for (int i = 0; i < UnitSpawnCellNeighbors.Count; i++)
            {
                ColorSingleCell(UnitSpawnCellNeighbors[i], Color.white);
            }

            currentUnitHasSpawned = false;

            UI.SpawnPanel.panel.SetActive(false);

            currentObjectState = ObjectHandler.Idle;
        }
    }

    //This exit state is empty as of now
    void ExitRotateUnit()
    {
        
        SetFlankCellCoordinates();

        currentObjectState = ObjectHandler.Idle;
    }

    void ExitUnitSpawn()
    {
        for (int i = 0; i < UnitSpawnCellNeighbors.Count; i++)
        {
            ColorSingleCell(UnitSpawnCellNeighbors[i], Color.white);
        }

        currentUnitToSpawn.GetComponent<Unit>().InitializeUnit(currentSpawnLevelIterator);

        currentUnitHasSpawned = false;

        UI.SpawnPanel.panel.SetActive(false);

        currentObjectState = ObjectHandler.InitiateUnitRotate;
    }

    void PathUnit()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            #region Mouse hits board
            if(hit.collider.tag == "Board")
            {
                AStarPathfind(hit.point);

                if (Input.GetMouseButtonDown(0))
                {
                    SetCellsToColor(previousPathElements, Color.white);

                    //This is an Action region function that is usually called through SelectionRaycast(). However, it does everything I need done here
                    NoSelection();
                }

                if (Input.GetMouseButtonDown(1) && HaveSufficientAPForMove())
                {
                    if(currentPathElements[currentPathElements.Length - 1] == currentTargetElement)
                        currentObjectState = ObjectHandler.MoveUnit;                                            
                }                    
            }
            #endregion

            #region Mouse hits player object
            else if ((hit.collider.tag == "Node" || hit.collider.tag == "Unit") && hit.collider.gameObject.layer == currentPlayer.layer)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    currentSelection = hit.collider.gameObject;
                    SetCellsToColor(previousPathElements, Color.white);
                    SwitchAction();
                }
            }
            #endregion
           
            else if (hit.collider.gameObject.layer == otherPlayer.layer)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    currentSelection = hit.collider.gameObject;
                    SetCellsToColor(previousPathElements, Color.white);
                    SwitchAction();
                }

                //This uses the capture AP check because it does the same check that is required for combat
                else if (Input.GetMouseButtonDown(1) && currentPlayer.currentAP >= currentSelection.GetComponent<Unit>().currentTier)
                {                    
                    //current target must be an enemy combatible piece. It is not to be confused with "currentSelection"
                    currentTarget = hit.collider.gameObject;
     
                    if (!CheckForTargetInNeighbors())
                    {
                        combatMove = true;
                        currentObjectState = ObjectHandler.MoveUnit;
                    }
                    else
                    {
                        CalculateRotationAngleFromVector3(currentTarget.transform.position);
                        currentSelection.GetComponent<Unit>().unitDirection = (int)neighborAngle;
                        SetFlankCellCoordinates();
                        currentObjectState = ObjectHandler.InitializeCombat;
                    }
                }
            }

            else if (hit.collider.tag == "Node")
            {
                AStarPathfind(hit.point);
                
                if (Input.GetMouseButtonDown(1) && HaveSufficientAPForCapture() && hit.collider.gameObject.layer == 10)
                {
                    capturingNode = true;
                    currentNodeBeingCaptured = hit.collider.gameObject;
                    currentObjectState = ObjectHandler.MoveUnit;
                }
            }
        }

        //The check for entering stationary rotation comes after the pathfind in order to actually have previousPathElements contain values
        if (Input.GetKeyDown(KeyCode.R))
        {
            SetCellsToColor(previousPathElements, Color.white);
            currentObjectState = ObjectHandler.InitiateUnitRotate;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            SetCellsToColor(previousPathElements, Color.white);
            EndTurn();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCellsToColor(previousPathElements, Color.white);
            currentSelection = default(GameObject);
            NoSelection();
            currentObjectState = ObjectHandler.Idle;
        }
    }

    void MoveUnit() 
    {
        ClearHexCellOfObject(currentSelection);

        if (currentPathElements.Length != 0)
        {
            movementTValue += Time.deltaTime;
            float tStep = movementTValue / 1;

            //This equation flushes the bottom of any unit tier with the surface of the hex board... It must be used a few times in the following code
            Vector3 heighAdjustedPosition = hexCells[currentPathElements[movementPathIterator]].cellPosition
                                            + ((Vector3.up / currentSelection.GetComponent<Unit>().hexSizeDivisor)
                                            * currentSelection.transform.localScale.y);
           
            //These following two lines will not change anything visually being called between the lerps, but there is room to optimize these calls
            CalculateRotationAngleFromVector3(heighAdjustedPosition);

            currentSelection.transform.position = Vector3.Lerp(unitMovementLerpStart, heighAdjustedPosition, tStep * globalMoveSpeed);

            if (currentSelection.transform.position == heighAdjustedPosition)
            {
                movementTValue = 0f;
                ColorSingleCell(ReturnHexCellElement(currentSelection.transform.position), Color.white);
                unitMovementLerpStart = currentSelection.transform.position;
                movementPathIterator++;

                currentPlayer.currentAP -= currentSelection.GetComponent<Unit>().currentTier;
            }
        }

        if(movementPathIterator >= currentPathElements.Length)
        {
            movementTValue = 0f;
            movementPathIterator = 0;

            if (combatMove)
            {
                combatMove = false;
                hexCells[ReturnHexCellElement(currentSelection.transform.position)].hasUnit = true;
                hexCells[ReturnHexCellElement(currentSelection.transform.position)].cellUnit = currentSelection;
                CalculateRotationAngleFromVector3(currentTarget.transform.position);                
                currentSelection.GetComponent<Unit>().unitDirection = (int)neighborAngle;
                SetFlankCellCoordinates();
                currentObjectState = ObjectHandler.InitializeCombat;
            }

            else if (!capturingNode)
            {                
                hexCells[ReturnHexCellElement(currentSelection.transform.position)].hasUnit = true;
                hexCells[ReturnHexCellElement(currentSelection.transform.position)].cellUnit = currentSelection;
                currentSelection.GetComponent<Unit>().unitDirection = (int)neighborAngle;
                SetFlankCellCoordinates();
                currentObjectState = ObjectHandler.PathUnit;
            }
            
            else
            {
                capturingNode = false;
                
                if (CheckForNodeInNeighbors())
                    BeginCaptureNodeForPlayer(currentPlayer);  
            }
        }
    }

    void InitalizeCombat()
    {
        doCounterAttack = false;

        if (currentTarget.GetComponent<Unit>() != null)
        {
            currentTargetType = typeof(Unit);
        }
        else if (currentTarget.GetComponent<Node>() != null)
        {
            currentTargetType = typeof(Node);
        }

        attackingUnit = currentSelection.GetComponent<Unit>(); 
        defendingUnit = currentTarget.GetComponent<Unit>();
        
        //Check if counter attack needs to be confirmed
        //Checks for units facing each other, as well as does an AP check on the defender
        if( CheckForFaceToFace(attackingUnit, defendingUnit) && 
            defendingUnit.owner.currentAP >= defendingUnit.currentTier)
        {
            currentObjectState = ObjectHandler.ConfirmCounterAttack;
        }
        else
        {
            currentObjectState = ObjectHandler.Combat;
        }        
    }

    void BattleFunction(Unit attacker, Unit defender)
    {
        if (doCounterAttack)
        {
            //This is a testing guard... Is not actually useful atm
            if (currentTargetType == typeof(Unit))
            {
                BattleDamage(attacker, defender);
                BattleDamage(defender, attacker);
                
                if (defender.hitPoints <= 0)
                {
                    DestroyUnit(defender.gameObject);
                    defenderSurvivedBattle = false;
                }

                if (attacker.hitPoints <= 0)
                {
                    DestroyUnit(attacker.gameObject);
                }
            }
        }
        else
        {
            BattleDamage(attacker, defender);

            if (defender.hitPoints <= 0)
            {
                DestroyUnit(defender.gameObject);
                defenderSurvivedBattle = false;
            }
        }

        if(defender.hitPoints > 0)
        {
            defenderSurvivedBattle = true;
        }

        attacker.owner.currentAP -= attacker.currentTier;

        if (defenderSurvivedBattle)
        {
            if (!doCounterAttack)
            {
                newAttacker = defender;

                if (newAttacker.owner.currentAP >= newAttacker.currentTier)
                {
                    if (ForwardFaceContainsEnemy(newAttacker))
                    {
                        if (CheckForFaceToFace(newAttacker, newDefender))//newDefender is assigned in the previous if's check function
                        {
                            attackingUnit = newAttacker;
                            defendingUnit = newDefender;

                            if (newDefender.owner.currentAP >= newDefender.currentTier)
                            {                                
                                currentObjectState = ObjectHandler.ConfirmCounterAttack;
                                //At this point in the game, this will not differentiate for chaning inputs between players... That will have to
                                //be handled once the game becomes multi-player... However, an AI can just do this automatically for its turn...
                            }
                            else
                            {                               
                                currentObjectState = ObjectHandler.Combat;
                            }
                        }
                    }
                    //If the forward face does not contain an enemy, then no counter can be made. The next action differs here depending on which
                    //player's unit is the attacker. If it is the active player's unit that finds no target in front of it, then attack priority
                    //passes to the next active player's unit, if there is one...
                    //However, if the attacking unit is the passive player's and it finds no target, then attack priority does NOT fall to the
                    //"attacker's" next unit; it falls to the next active player's unit in the sequence... This is designed to keep initiative with
                    //the attacking player.
                }
            }
        }

        //If there is a friendly unit adjacent to the current selection, it gains priority as the new attacker.
        //This will execute if any of the above checks fail, as making it through all the checks will change the object state.
        //Keep in mind, this only works for the attacking player atm... The design decision is to keep initiative away from the defending player.

        if (useAutoSequenceForBattleChain)
        {            
            //This code is only reached if the object state does not change, which means priority of attack must fall to another unit of the active player
            attackingUnit = SearchForNextUnitInChain(attacker);
        }
        //This code COULD only include units in the attack chain that can actually perform an attack. A blind search may yield an irrelevant case.
        //This is where some kind of list could come into play, as a search could yield multiple targets...
        //I feel like most scenarios will handle themselves pretty well. I am being very picky about possible edge cases...


        //This is where facilitating manual unit chaining will come into the code
    }

    bool ForwardFaceContainsEnemy(Unit unit)
    {
        bool localBool = false;

        GameObject forwardCellObject = ReturnCellUnitInUnitFlank(unit, Combatable.Flank.Forward);

        if ( forwardCellObject != null)
        {
            if(forwardCellObject.layer != unit.owner.layer)
            {
                localBool = true;
                newDefender = forwardCellObject.GetComponent<Unit>();
            }
        }

        return localBool;
    }

    GameObject ReturnCellUnitInUnitFlank(Unit unit, Combatable.Flank flank)
    {
        return hexCells[ReturnHexCellElement((int)unit.flankPositions[flank].x, (int)unit.flankPositions[flank].y)].cellUnit;
    }

    Unit SearchForNextUnitInChain(Unit unit)
    {
        Unit localUnit = unit;

        CheckForAlliesWithTarget(CheckNeighborsForAlly(unit));

        return localUnit;
    }

    void CheckForAlliesWithTarget(List<GameObject> allyList)
    {
        for (int i = 0; i < allyList.Count; i++)
        {
            Unit localUnit = allyList[i].GetComponent<Unit>();

            GameObject unitTarget = ReturnCellUnitInUnitFlank(localUnit, Combatable.Flank.Forward);
            
            //This check uses layers to determine if the target is an enemy and not a neutral node
            if(unitTarget.layer != localUnit.gameObject.layer && unitTarget.layer != 10 && unitTarget != null)
            {

            }            
        }
    }

    List<GameObject> CheckNeighborsForAlly(Unit attacker)
    {
        List<GameObject> neighboredAllies = new List<GameObject>();

        for (int i = 1; i < 6; i++)
        {
            GameObject localObject = ReturnCellUnitInUnitFlank(attacker, (Combatable.Flank)i);

            if (localObject != null && localObject.layer == attacker.owner.layer)
            {
                neighboredAllies.Add(localObject);
            }
        }

        return neighboredAllies;
    }

    void Combat()
    {
        BattleFunction(attackingUnit, defendingUnit);

        //Check for if the defender survived, and if they have counter-attacked?

        currentObjectState = ObjectHandler.Idle;
    }

    //One of these must be done to progress the game state
    void ConfirmCounterAttack()
    {
        Debug.Log("Counter attack?");

        //Need to add some kind of UI pop-up here
        if (Input.GetKeyDown(KeyCode.C))
        {
            doCounterAttack = true;
            defendingUnit.owner.currentAP -= defendingUnit.currentTier;
            currentObjectState = ObjectHandler.Combat;
        }

        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            doCounterAttack = false;
            currentObjectState = ObjectHandler.Combat;
        }        
    }

    //Destroys a unit, and marks its cell as passable
    void DestroyUnit(GameObject unitToDestroy)
    {
        ClearHexCellOfObject(unitToDestroy);
        Destroy(unitToDestroy);
    }

    void UpgradeNode()
    {
        Node currentNode = currentSelection.GetComponent<Node>();

        if (currentPlayer.currentAP >= currentNode.GetUpgradeCost() && globalMaxTier > currentNode.currentTier)
        {
            currentPlayer.currentAP -= currentNode.GetUpgradeCost();

            currentNode.isCurrentlyUpgrading = true;

            currentNode.currentUpgradeIterator = 1;

            UpdateNodePanelValues();
        }

        currentObjectState = ObjectHandler.Idle;
    }

    public void CallInitiateUnitSpawn()
    {
        currentObjectState = ObjectHandler.InitiateUnitSpawn;
    }

    public void CallUpgradeNode()
    {
        currentObjectState = ObjectHandler.UpgradeNode;
    }

    #endregion

    #region Player State

    void PlayerOneInitialize()
    {
        PlayerOne.CalculateCurrentCaptureStates();

        PlayerOne.CalculateActionPointsForTurn();
        
        UI.playerColorOutlinePanel.GetComponent<Image>().color = playerOneColor;

        otherPlayer = PlayerTwo;

        currentPlayer = PlayerOne;

        UpdateNodeTiering();

        currentPlayerColor = playerOneColor;

        currentPlayerState = PlayerState.PLAYER_ONE_ACTIVE;
    }

    void PlayerTwoInitialize()
    {       
        PlayerTwo.CalculateCurrentCaptureStates();

        PlayerTwo.CalculateActionPointsForTurn();

        UI.playerColorOutlinePanel.GetComponent<Image>().color = playerTwoColor;

        otherPlayer = PlayerOne;

        currentPlayer = PlayerTwo;

        UpdateNodeTiering();

        currentPlayerColor = playerTwoColor;

        currentPlayerState = PlayerState.PLAYER_TWO_ACTIVE;
    }

    void PlayerOneActive()
    {
        UI.textAP.GetComponent<Text>().text = "AP: " + PlayerOne.currentAP;

        //PlayerOne.currentAP = 999;

        osm[currentObjectState].Invoke();  
    }

    void PlayerTwoActive()
    {
        UI.textAP.GetComponent<Text>().text = "AP: " + PlayerTwo.currentAP;
    
        osm[currentObjectState].Invoke();
    }

    void PlayerOneExit()
    {
        NoSelection();

        currentObjectState = ObjectHandler.Idle;
        currentPlayerState = PlayerState.PLAYER_TWO_INITIALIZE;
    }

    void PlayerTwoExit()
    {
        NoSelection();

        currentObjectState = ObjectHandler.Idle;
        currentPlayerState = PlayerState.PLAYER_ONE_INITIALIZE;
    }

    #endregion

    #endregion

    #endregion

    #region Defaults

    void Awake()
    {
        playerOneToggles = UI.playerOneColorPanel.GetComponentsInChildren<Toggle>();
        playerTwoToggles = UI.playerTwoColorPanel.GetComponentsInChildren<Toggle>();

        UI.playerTwoColorPanel.SetActive(false);

        BuildBoard();

        PlayerOne = new Player(playerOneMaterial, boardData.playerOneStartElement, hexCells, playerOneColor, playerOneLayer);

        PlayerTwo = new Player(playerTwoMaterial, boardData.playerTwoStartElement, hexCells, playerTwoColor, playerTwoLayer);

        fsm.Add(PlayerState.PLAYER_ONE_INITIALIZE, new Action(PlayerOneInitialize));
        fsm.Add(PlayerState.PLAYER_TWO_INITIALIZE, new Action(PlayerTwoInitialize));
        fsm.Add(PlayerState.PLAYER_ONE_ACTIVE, new Action(PlayerOneActive));
        fsm.Add(PlayerState.PLAYER_TWO_ACTIVE, new Action(PlayerTwoActive));
        fsm.Add(PlayerState.PLAYER_ONE_EXIT, new Action(PlayerOneExit));
        fsm.Add(PlayerState.PLAYER_TWO_EXIT, new Action(PlayerTwoExit));

        osm.Add(ObjectHandler.Idle, new Action(Idle));
        osm.Add(ObjectHandler.InitiateUnitRotate, new Action(InitiateUnitRotate));
        osm.Add(ObjectHandler.InitiateUnitSpawn, new Action(InitiateUnitSpawn));
        osm.Add(ObjectHandler.RotateUnit, new Action(RotateUnit));
        osm.Add(ObjectHandler.SpawnUnit, new Action(SpawnUnit));
        osm.Add(ObjectHandler.ExitRotateUnit, new Action(ExitRotateUnit));
        osm.Add(ObjectHandler.ExitUnitSpawn, new Action(ExitUnitSpawn));
        osm.Add(ObjectHandler.PathUnit, new Action(PathUnit));
        osm.Add(ObjectHandler.MoveUnit, new Action(MoveUnit));
        osm.Add(ObjectHandler.InitializeCombat, new Action(InitalizeCombat));
        osm.Add(ObjectHandler.Combat, new Action(Combat));
        osm.Add(ObjectHandler.ConfirmCounterAttack, new Action(ConfirmCounterAttack));
        osm.Add(ObjectHandler.UpgradeNode, new Action(UpgradeNode));

        currentPlayerState = PlayerState.PLAYER_ONE_INITIALIZE;
        currentObjectState = ObjectHandler.Idle;
    }

    void Start()
    {
         
    }

    void Update()
    {
        fsm[currentPlayerState].Invoke();

        if (Input.GetKeyDown(KeyCode.F)) {

            Debug.Log(currentPathElements.Length);
        }
    }

    #endregion Defaults

    #region Functions

    #region Actions

    #region HandleObject

    void HandleNode()
    {
        UI.UnitPanel.unitPanel.SetActive(false);
        UI.NodePanel.nodePanel.SetActive(true);

        UpdateNodePanelValues();

        if(currentSelection.layer == currentPlayer.layer)
            currentActionNode = currentSelection;

        currentObjectState = ObjectHandler.Idle;
    }

    void HandleUnit()
    {
        UI.NodePanel.nodePanel.SetActive(false);
        UI.UnitPanel.unitPanel.SetActive(true);

        unitMovementLerpStart = currentSelection.transform.position;

        UpdateUnitPanelValues();

        if (currentPlayer.layer == currentSelection.layer)
            currentObjectState = ObjectHandler.PathUnit;
        else
            currentObjectState = ObjectHandler.Idle;
    }

    void NoSelection()
    {
        UI.NodePanel.nodePanel.SetActive(false);
        UI.UnitPanel.unitPanel.SetActive(false);
        currentSelection = default(GameObject);

        currentObjectState = ObjectHandler.Idle;
    }

    #endregion 

    void BeginCaptureNodeForPlayer(Player player)
    {
        player.currentAP -= currentSelection.GetComponent<Unit>().currentTier * 2;

        currentNodeBeingCaptured.GetComponent<Node>().currentCaptureIterator = currentSelection.GetComponent<Unit>().currentTier;

        currentNodeBeingCaptured.GetComponent<Node>().currentTier = currentSelection.GetComponent<Unit>().currentTier;

        player.AddToNodeInCapture(currentNodeBeingCaptured);

        player.unitCollection.Remove(currentSelection);

        Destroy(currentSelection);

        currentSelection = currentNodeBeingCaptured;

        HandleNode();
    }

    void ClearHexCellOfObject(GameObject obj)
    {
        hexCells[ReturnHexCellElement(obj.transform.position)].hasUnit = false;

        hexCells[ReturnHexCellElement(obj.transform.position)].cellUnit = default(GameObject);
    }
    void SetHexCellWithObject(GameObject obj)
    {
        hexCells[ReturnHexCellElement(obj.transform.position)].hasUnit = true;

        hexCells[ReturnHexCellElement(obj.transform.position)].cellUnit = obj;
    }

    void SwitchAction()
    {
        switch (currentSelection.tag)
        {
            case "Node":

                HandleNode();

                break;

            case "Unit":

                HandleUnit();
                
                break;

            case "Board":

                NoSelection();

                break;
        }
    }

    #endregion

    #region Board Functionality

    void AssignColoredOutline(int element)
    {
        //ColorSingleCell(element, Color.white);

        for (int i = element * 7, j = i + 7; i < j; i++)        
            boardUVs[i] += new Vector2(.5f, 0f);

        hexBoardMesh.uv = boardUVs.ToArray();        
    }

    bool CheckForNodeInNeighbors()
    {
        bool nodeInNeighbors = false;

        for (int i = 0; i < 6; i++)
        {
            int newCubeX = hexCells[ReturnHexCellElement(currentSelection.transform.position)].cubeX + (int)neighborDirection[i].x;
            int newCubeZ = hexCells[ReturnHexCellElement(currentSelection.transform.position)].cubeZ + (int)neighborDirection[i].y;

            int neighborElement = ReturnHexCellElement(newCubeX, newCubeZ);

            if (hexCells[neighborElement].cellNode == currentNodeBeingCaptured)
            {
                nodeInNeighbors = true;
            }
        }

        return nodeInNeighbors;
    }

    bool CheckForTargetInNeighbors()
    {
        bool targetInNeighbors = false;

        for (int i = 0; i < 6; i++)
        {
            int newCubeX = hexCells[ReturnHexCellElement(currentSelection.transform.position)].cubeX + (int)neighborDirection[i].x;
            int newCubeZ = hexCells[ReturnHexCellElement(currentSelection.transform.position)].cubeZ + (int)neighborDirection[i].y;

            int neighborElement = ReturnHexCellElement(newCubeX, newCubeZ);

            if (hexCells[neighborElement].cellUnit == currentTarget)
            {
                targetInNeighbors = true;
            }
        }

        return targetInNeighbors;
    }

    public List<int> GetCellNeighbors(HexCell cell)
    {
        List<int> neighbors = new List<int>();

        int cellElement = ReturnHexCellElement(cell);

        for (int i = 0; i < 6; i++)
        {
            int newCubeX = hexCells[cellElement].cubeX + (int)neighborDirection[i].x;
            int newCubeZ = hexCells[cellElement].cubeZ + (int)neighborDirection[i].y;

            int neighborElement = ReturnHexCellElement(newCubeX, newCubeZ);

            neighbors.Add(neighborElement);
        }

        return neighbors;
    }

    public List<int> GetCellNeighborsForPathing(HexCell cell)
    {
        List<int> neighbors = new List<int>();

        int cellElement = ReturnHexCellElement(cell);

        for (int i = 0; i < 6; i++)
        {
            int newCubeX = hexCells[cellElement].cubeX + (int)neighborDirection[i].x;
            int newCubeZ = hexCells[cellElement].cubeZ + (int)neighborDirection[i].y;

            int neighborElement = ReturnHexCellElement(newCubeX, newCubeZ);

            if (hexCells[neighborElement].hasNode || hexCells[neighborElement].hasObstacle || hexCells[neighborElement].hasUnit)
            {
                continue;
            }

            else
                neighbors.Add(neighborElement);
        }

        return neighbors;
    }

    Vector2 CalculateCellDirectionFromVector2ForTarget(Vector2 v2)
    {
        Vector3 localVector3 = new Vector3(v2.x, 0f, v2.y);

        int hitCellCubeX = hexCells[ReturnHexCellElement(localVector3)].cubeX;
        int hitCellCubeZ = hexCells[ReturnHexCellElement(localVector3)].cubeZ;

        int unitCellCubeX = hexCells[ReturnHexCellElement(currentTarget.transform.position)].cubeX;
        int unitCellCubeZ = hexCells[ReturnHexCellElement(currentTarget.transform.position)].cubeZ;

        Vector2 hitCell = new Vector2(hitCellCubeX, hitCellCubeZ);
        Vector2 unitCell = new Vector2(unitCellCubeX, unitCellCubeZ);

        return hitCell - unitCell;
    }

    Vector2 CalculateCellDirectionFromVector3(Vector3 hit)
    {
        int hitCellCubeX = hexCells[ReturnHexCellElement(hit)].cubeX;
        int hitCellCubeZ = hexCells[ReturnHexCellElement(hit)].cubeZ;

        int unitCellCubeX = hexCells[ReturnHexCellElement(currentSelection.transform.position)].cubeX;
        int unitCellCubeZ = hexCells[ReturnHexCellElement(currentSelection.transform.position)].cubeZ;

        Vector2 hitCell = new Vector2(hitCellCubeX, hitCellCubeZ);
        Vector2 unitCell = new Vector2(unitCellCubeX, unitCellCubeZ);

        return hitCell - unitCell;
    }

    void CalculateRotationAngleFromVector3(Vector3 hit)
    {
        Vector2 hitDirection = CalculateCellDirectionFromVector3(hit);
      
        CheckVector2ForNeighborAngle(hitDirection);

        SetRotationFromNeighborAngle();
    }

    void CheckVector2ForNeighborAngle(Vector2 hitVector)
    {
        for (int i = 0; i < 6; i++)
        {
            if (hitVector == neighborDirection[i])
                neighborAngle = (HexNeighbor)i;
        }
    }

    //void ReturnNeighborEnumFromVector2(Vector2 direction)    

    public void BuildBoard()
    {
        GetComponent<MeshFilter>().mesh = hexBoardMesh = new Mesh();
        hexBoardMesh.name = "Hex Board";
        hexBoardCollider = gameObject.AddComponent<MeshCollider>();

        #region Initializations

        boardColors = new List<Color>();
        boardTriangles = new List<int>();
        boardUVs = new List<Vector2>();
        boardVertices = new List<Vector3>();

        hexCells = new HexCell[boardSizeX * boardSizeY];

        for (int i = 0; i < hexCells.Length; i++)
        {
            hexCells[i] = JsonUtility.FromJson<HexCell>(boardData.JSONData[i]);
        }

        #endregion List Initializations

        #region Clear Data

        hexBoardMesh.Clear();
        boardColors.Clear();
        boardTriangles.Clear();
        boardUVs.Clear();
        boardVertices.Clear();

        #endregion

        for (int i = 0; i < hexCells.Length; i++)
        {
            int vertexIndex = boardVertices.Count;

            if (hexCells[i].hasNode)
            {
                hexCells[i].cellNode = Instantiate(nodePrefab, hexCells[i].cellPosition, Quaternion.identity, transform) as GameObject;
                hexCells[i].cellNode.gameObject.GetComponent<Node>().nodeCellElement = ReturnHexCellElement(hexCells[i]);
            }

            for (int j = 0; j < HexData.corners.Length; j++)
            {
                boardVertices.Add(HexData.corners[j] + hexCells[i].cellPosition);
                boardUVs.Add(new Vector2(HexData.UVs[j].x / 2, HexData.UVs[j].y));

                if (hexCells[i].hasObstacle && hexCells[i].hasNode != true)
                    boardColors.Add(Color.black);
                else
                    boardColors.Add(Color.white);

                if (j == 0)
                    continue;

                boardTriangles.Add(vertexIndex);
                boardTriangles.Add(vertexIndex + j);
                if (j == 6)
                    boardTriangles.Add(vertexIndex + 1);
                else
                    boardTriangles.Add(vertexIndex + j + 1);
            }
        }

        #region Set Mesh Values

        hexBoardMesh.vertices = boardVertices.ToArray();
        hexBoardMesh.colors = boardColors.ToArray();
        hexBoardMesh.uv = boardUVs.ToArray();
        hexBoardMesh.triangles = boardTriangles.ToArray();
        hexBoardMesh.RecalculateNormals();

        hexBoardCollider.sharedMesh = hexBoardMesh;

        #endregion        

        EditorUtility.SetDirty(boardData);
    }

    public void ColorSingleCell(int cellElement, Color color)
    {        
        for (int i = cellElement * 7, j = i + 7;  i < j; i++)
        {
            boardColors[i] = color;
        }

        hexBoardMesh.colors = boardColors.ToArray();
    }

    public void GenerateHexCellData()
    {
        hexCells = new HexCell[boardSizeX * boardSizeY];

        string[] hexCellsJSON = new string[hexCells.Length];

        for (int z = 0, i = 0; z < boardSizeY; z++)
        {
            for (int x = 0; x < boardSizeX; x++, i++)
            {
                Vector3 hexCellPosition;

                hexCellPosition.x = (x + z * 0.5f - z / 2) * HexData.innerRadius * 2f;
                hexCellPosition.y = 0f;
                hexCellPosition.z = (z * HexData.outerRadius * 1.5f);

                HexCell cell = hexCells[i] = new HexCell(hexCellPosition);

                string cellJSON = JsonUtility.ToJson(cell);

                hexCellsJSON[i] = cellJSON;
            }
        }

        boardData.JSONData = hexCellsJSON;
        EditorUtility.SetDirty(boardData);
    }

    public void RemoveHexBoardData()
    {
        GetComponent<MeshFilter>().mesh = null;
        DestroyImmediate(GetComponent<MeshCollider>());

        List<Node> nodes = new List<Node>();

        nodes.AddRange(gameObject.GetComponentsInChildren<Node>());

        foreach (Node node in nodes)
        {
            DestroyImmediate(node.gameObject);
        }
    }

    #region ReturnHexCellElement
    
    public int ReturnHexCellElement(Vector3 position)
    {
        int element;

        float x = position.x / (HexData.innerRadius * 2f);
        float y = -x;

        float offset = position.z / (HexData.outerRadius * 3f);
        x -= offset;
        y -= offset;

        int iX = Mathf.RoundToInt(x);
        int iY = Mathf.RoundToInt(y);
        int iZ = Mathf.RoundToInt(-x - y);

        if (iX + iY + iZ != 0)
        {
            float dX = Mathf.Abs(x - iX);
            float dY = Mathf.Abs(y - iY);
            float dZ = Mathf.Abs(-x - y - iZ);

            if (dX > dY && dX > dZ)
            {
                iX = -iY - iZ;
            }
            else if (dZ > dY)
            {
                iZ = -iX - iY;
            }
        }

        element = iX + iZ * boardSizeX + iZ / 2;

        return element;
    }

    public int ReturnHexCellElement(HexCell cell)
    {
        return cell.cubeX + cell.cubeZ * boardSizeX + cell.cubeZ / 2;
    }

    public int ReturnHexCellElement(int cellX, int cellZ)
    {
        return cellX + cellZ * boardSizeX + cellZ / 2;
    }

    #endregion

    #region SetCellsToColor

    public void SetCellsToColor(List<int> elementsToColor, Color color)
    {
        //Does not color the cell the unit starts on
        for (int i = 0; i < elementsToColor.Count; i++)
        {
            for (int j = elementsToColor[i] * 7, k = j + 7; j < k; j++)
            {
                boardColors[j] = color;
            }
        }

        hexBoardMesh.colors = boardColors.ToArray();
    }

    public void SetCellsToColor(int[] elementsToColor, Color color)
    {
        //Does not color the cell the unit starts on
        for (int i = 0; i < elementsToColor.Length; i++)
        {
            for (int j = elementsToColor[i] * 7, k = j + 7; j < k; j++)
            {
                boardColors[j] = color;
            }
        }

        hexBoardMesh.colors = boardColors.ToArray();
    }

    public void SetCellsToColor(List<int> elementsToColor, Color color, int AP)
    {
        //Does not color the cell the unit starts on
        for (int i = 0; i < AP; i++)
        {            
            for (int j = elementsToColor[i] * 7, k = j + 7; j < k; j++)
            {
                boardColors[j] = color;
            }
        }

        hexBoardMesh.colors = boardColors.ToArray();
    }

    public void SetCellsToColor(int[] elementsToColor, Color color, int AP)
    {
        //Does not color the cell the unit starts on
        for (int i = 0; i < AP; i++)
        {
            for (int j = elementsToColor[i] * 7, k = j + 7; j < k; j++)
            {
                boardColors[j] = color;
            }
        }

        hexBoardMesh.colors = boardColors.ToArray();
    }

    #endregion

    #endregion

    #region Combat

    void BattleDamage(Unit attacker, Unit defender)
    {
        int flankArmorInt = new int();

        for (int i = 0; i < 6; i++)
        {
            if(attacker.cellCoordinate == defender.flankPositions[(Combatable.Flank)i])
            {
                flankArmorInt = i;
                Debug.Log(flankArmorInt);
            }
        }

        int damageAfterArmor = attacker.currentTier - defender.flankArmor[flankArmorInt];

        defender.hitPoints -= damageAfterArmor;        
    }

    //Returns if currentSelection and currentTarget are occupying each other's forward cells
    bool CheckForFaceToFace(Unit attacker, Unit defender)
    {
        return attacker.flankPositions[Combatable.Flank.Forward] ==
               defender.cellCoordinate &&
               defender.flankPositions[Combatable.Flank.Forward] ==
               attacker.cellCoordinate;
    }

    //Works off currentSelection
    void SetFlankCellCoordinates()
    {       
        int currentSelectionCubeX = hexCells[ReturnHexCellElement(currentSelection.transform.position)].cubeX;
        int currentSelectionCubeZ = hexCells[ReturnHexCellElement(currentSelection.transform.position)].cubeZ;
        
        currentSelection.GetComponent<Unit>().flankPositions.Clear();

        //Assign current selection neighbors and flank directions
        for (int i = 0; i < 6; i++)
        {
            int adjustedNeighborInt = currentSelection.GetComponent<Unit>().unitDirection + i;

            //This check allows the loop to start at zero, yet still adhere to the starting rotation of the currentSelection
            if (adjustedNeighborInt > 5)
            {
                adjustedNeighborInt = currentSelection.GetComponent<Unit>().unitDirection + i - neighborDirection.Length;
            }
            
            Vector2 flankCoordinate = new Vector2(currentSelectionCubeX, currentSelectionCubeZ) + neighborDirection[adjustedNeighborInt];

            currentSelection.GetComponent<Unit>().flankPositions.Add((Combatable.Flank)i, flankCoordinate);
        }

        //Assigns the current combatable's cell coordinate using the calculated cubeZ and cubeZ values from above.
        //This code may need to be included somewhere else at a later date.
        currentSelection.GetComponent<Unit>().cellCoordinate = new Vector2(currentSelectionCubeX, currentSelectionCubeZ);

        /*
        int cellToColor = ReturnHexCellElement( (int)currentSelection.GetComponent<Unit>().flankPositions[Combatable.Flank.Forward].x,
                                                (int)currentSelection.GetComponent<Unit>().flankPositions[Combatable.Flank.Forward].y);

        ColorSingleCell(cellToColor, Color.green);        
        */
    }

    #endregion

    #endregion

    #region Generic Types



    #endregion

    #region Node

    void UpdateNodeTiering()
    {
        for (int i = 0; i < currentPlayer.nodeCollection.Count; i++)
        {
            Node localNode = currentPlayer.nodeCollection[i].GetComponent<Node>();

            if (localNode.isCurrentlyUpgrading) {

                localNode.currentUpgradeIterator++;

                if(localNode.currentTier + 1 == localNode.currentUpgradeIterator)
                {
                    localNode.isCurrentlyUpgrading = false;

                    localNode.currentTier++;

                    localNode.currentUpgradeIterator = 0;

                    localNode.transform.localScale += Vector3.one * localNode.scaleFactor;

                    UpdateNodePanelValues();
                }
            }
        }
    }

    #endregion

    #region Pathfinding

    bool HaveSufficientAPForCapture()
    {
        return currentPlayer.currentAP >= ((currentPathElements.Length * currentSelection.GetComponent<Unit>().currentTier) + currentSelection.GetComponent<Unit>().currentTier * 2);
    }

    bool HaveSufficientAPForMove()
    {
        return currentPathElements.Length <= currentPlayer.currentAP / currentSelection.GetComponent<Unit>().currentTier;
    }

    int DistanceBetweenCells(int A, int B)
    {
        int dstX = Mathf.Abs(hexCells[A].cubeX - hexCells[B].cubeX);
        int dstZ = Mathf.Abs(hexCells[A].cubeZ - hexCells[B].cubeZ);

        return dstX + dstZ;
    }

    void AStarPathfind(Vector3 target)
    {
        int unitElement = ReturnHexCellElement(currentSelection.transform.position);
        int targetElement = ReturnHexCellElement(target);

        currentTargetElement = targetElement;

        List<int> coordsToCheck = new List<int>();

        HashSet<int> checkedCoords = new HashSet<int>();

        coordsToCheck.Add(unitElement);

        while (coordsToCheck.Count > 0)
        {
            int currentElement = coordsToCheck[0];

            //This will always skip the very first check, which is needed since no costs have been assigned yet
            for (int i = 1; i < coordsToCheck.Count; i++)
            {
                //gCost is distance from start, hCost is distance from target, fCost is sum of gCost and hCost
                if (hexCells[coordsToCheck[i]].fCost < hexCells[currentElement].fCost || hexCells[coordsToCheck[i]].fCost == hexCells[currentElement].fCost && hexCells[coordsToCheck[i]].hCost < hexCells[currentElement].hCost)
                    currentElement = coordsToCheck[i];
            }

            coordsToCheck.Remove(currentElement);
            checkedCoords.Add(currentElement);

            if(currentElement == targetElement)
            {
                RetracePath(unitElement, targetElement);
                return;
            }

            List<int> neighborElements = new List<int>();

            neighborElements = GetCellNeighborsForPathing(hexCells[currentElement]);
            
            foreach(int element in neighborElements)
            {
                if (checkedCoords.Contains(element))
                    continue;

                int distanceToNeighbor = hexCells[currentElement].gCost + 1; //Distance to any neighbor from a hex cell is '1'

                if(distanceToNeighbor < hexCells[element].gCost || !coordsToCheck.Contains(element))
                {
                    hexCells[element].gCost = distanceToNeighbor;
                    hexCells[element].hCost = DistanceBetweenCells(element, targetElement);

                    if (element == targetElement)
                        hexCells[targetElement].parentElement = currentElement;

                    else
                        hexCells[element].parentElement = currentElement;

                    if (!coordsToCheck.Contains(element))
                        coordsToCheck.Add(element);
                }
            }
        }
    }

    void ColorCurrentPath(List<int> pathElements)
    {        
        if(previousPathElements == null)
        {
            previousPathElements = new int[pathElements.Count];

            SetCellsToColor(pathElements, currentPlayerColor);

            previousPathElements = pathElements.ToArray();

            return;
        }

        if (previousPathElements == pathElements.ToArray())
            return;

        /*
        if (Input.GetMouseButtonDown(0))
        {
            SetCellsToColor(pathElements, Color.white);
            currentObjectState = ObjectHandler.Idle;
            return;
        }
        */

        if (pathElements.Count > currentPlayer.currentAP)
            return;

        SetCellsToColor(previousPathElements, Color.white);

        SetCellsToColor(pathElements, currentPlayerColor);

        previousPathElements = pathElements.ToArray();
    }
    
    void RetracePath(int start, int end)
    {
        List<int> pathElements = new List<int>();

        int currentElement = end;

        while(!(currentElement == start))
        {
            pathElements.Add(currentElement);
            currentElement = hexCells[currentElement].parentElement;
        }

        pathElements.Reverse();

        currentPathElements = new int[pathElements.Count];
        currentPathElements = pathElements.ToArray();

        if(HaveSufficientAPForMove())
            ColorCurrentPath(pathElements);
    }

    #endregion

    #region Rotation

    void SetRotationFromNeighborAngle()
    {
        //The only thing chaning in these cases is the value of y, in increments of 60 degrees
        switch (neighborAngle)
        {
            case HexNeighbor.UP_RIGHT:
                currentSelection.transform.eulerAngles = new Vector3(currentSelection.transform.rotation.x, 0f, currentSelection.transform.rotation.z);
                break;
            case HexNeighbor.RIGHT:
                currentSelection.transform.eulerAngles = new Vector3(currentSelection.transform.rotation.x, 60f, currentSelection.transform.rotation.z);
                break;
            case HexNeighbor.DOWN_RIGHT:
                currentSelection.transform.eulerAngles = new Vector3(currentSelection.transform.rotation.x, 120f, currentSelection.transform.rotation.z);
                break;
            case HexNeighbor.DOWN_LEFT:
                currentSelection.transform.eulerAngles = new Vector3(currentSelection.transform.rotation.x, 180f, currentSelection.transform.rotation.z);
                break;
            case HexNeighbor.LEFT:
                currentSelection.transform.eulerAngles = new Vector3(currentSelection.transform.rotation.x, 240f, currentSelection.transform.rotation.z);
                break;
            case HexNeighbor.UP_LEFT:
                currentSelection.transform.eulerAngles = new Vector3(currentSelection.transform.rotation.x, 300f, currentSelection.transform.rotation.z);
                break;
        }
    }

    #endregion

    #region Selection

    //PlayerOne = 8
    //PlayerTwo = 9
    void SelectionRaycast()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit))
            {
                if (hit.collider.tag == "Node" || hit.collider.tag == "Unit" || hit.collider.tag == "Board")                
                    currentSelection = hit.collider.gameObject;
                
                SwitchAction();
            }
        }
    }

    #endregion

    #region Spawning

    public void ColorSpawnFromAPCheck()
    {
        if (currentSpawnLevelIterator > currentPlayer.currentAP)
            currentUnitToSpawn.GetComponent<Renderer>().material.color = Color.gray;
        else
            currentUnitToSpawn.GetComponent<Renderer>().material.color = currentPlayerColor;
    }

    public void HandleSpawnTier(int increment)
    {
        int previousSpawnLevelIterator = currentSpawnLevelIterator;

        currentSpawnLevelIterator += increment;

        currentSpawnLevelIterator = (int)Mathf.Clamp(currentSpawnLevelIterator, 1f, currentSelection.GetComponent<Node>().currentTier);

        UI.SpawnPanel.spawnLevel.text = currentSpawnLevelIterator.ToString();

        if (previousSpawnLevelIterator == currentSpawnLevelIterator)
            currentSpawnIteratorHasChanged = false;
        else
            currentSpawnIteratorHasChanged = true;

        if (currentPlayer.currentAP < currentSpawnLevelIterator)
            UI.SpawnPanel.spawnLevel.GetComponent<Text>().color = Color.gray;
        else
            UI.SpawnPanel.spawnLevel.GetComponent<Text>().color = Color.black;
    }

    public void SpawnTierPanelUpdate()
    {
        if (Input.GetAxis("Mouse ScrollWheel") == 0.1f)
            HandleSpawnTier(1);

        if (Input.GetAxis("Mouse ScrollWheel") == -0.1f)
            HandleSpawnTier(-1);
    }

    #endregion

    #region UI

    public void AssignPlayerOneColor()
    {
        for (int i = 0; i < playerOneToggles.Length; i++)
        {
            if (playerOneToggles[i].GetComponent<Toggle>().isOn)
            {

                playerOneColor = playerOneToggles[i].GetComponent<Toggle>().GetComponentInChildren<Text>().color;
                playerTwoToggles[i].GetComponent<Toggle>().interactable = false;
            }
        }
    }

    public void AssignPlayerTwoColor()
    {
        for (int i = 0; i < playerTwoToggles.Length; i++)
        {
            if (playerTwoToggles[i].GetComponent<Toggle>().isOn)
            {

                playerTwoColor = playerTwoToggles[i].GetComponent<Toggle>().GetComponentInChildren<Text>().color;
                playerTwoToggles[i].GetComponent<Toggle>().interactable = false;
            }
        }
    }

    public void EndTurn()
    {
        switch (currentPlayerState)
        {
            case PlayerState.PLAYER_ONE_ACTIVE:
                currentPlayerState = PlayerState.PLAYER_ONE_EXIT;
                break;
            case PlayerState.PLAYER_TWO_ACTIVE:
                currentPlayerState = PlayerState.PLAYER_TWO_EXIT;
                break;
        }
    }

    public void SwapToSecondPanel()
    {
        UI.playerOneColorPanel.SetActive(false);
        UI.playerTwoColorPanel.SetActive(true);
    }

    void UpdateNodePanelValues()
    {
        Node localNode = currentSelection.GetComponent<Node>();

        UI.NodePanel.nodeTier.text = "Node Tier: " + localNode.currentTier;
        UI.NodePanel.tierCost.text = "Tier Cost: " + localNode.GetUpgradeCost();
        UI.NodePanel.upgradeStatus.text = "Tiering Status: " + localNode.currentUpgradeIterator + " / " + (localNode.currentTier + 1);
    }

    void UpdateUnitPanelValues()
    {
        Unit localUnit = currentSelection.GetComponent<Unit>();

        UI.UnitPanel.unitTier.text = "Unit Tier: " + localUnit.currentTier;
        //UI.UnitPanel.unitHealth.text = "Unit Health: " + localUnit.hitPoints;
    }

    #endregion

    #region Game Classes

    public class Combatable : MonoBehaviour
    {
        public Dictionary<Flank, Vector2> flankPositions;

        public enum Flank { Forward, LeftForward, LeftRear, Rear, RightRear, RightForward }

        public int currentTier;
        
        public int hitPoints;

        public Player owner;

        public Vector2 cellCoordinate;       
    }

    public class HexCell
    {
        #region Variables

        #region Cube Coordinates

        public int cubeX;
        public int cubeY;
        public int cubeZ;
        
        #endregion

        public bool hasNode;
        public bool hasObstacle;
        public bool hasUnit;        

        public GameObject cellNode;
        public GameObject cellUnit;

        #region Pathfinding values

        public int gCost;
        public int hCost;
        public int fCost
        {
            get { return gCost + hCost; }
        }

        public int parentElement;

        #endregion

        public Vector3 cellPosition;

        #endregion

        #region Constructors

        public HexCell(Vector3 position)
        {
            cellPosition = position;

            #region Constructs Cube Coordinates

            float x = position.x / (HexData.innerRadius * 2f);
            float y = -x;

            float offset = position.z / (HexData.outerRadius * 3f);
            x -= offset;
            y -= offset;

            int iX = Mathf.RoundToInt(x);
            int iY = Mathf.RoundToInt(y);
            int iZ = Mathf.RoundToInt(-x - y);

            if (iX + iY + iZ != 0)
            {
                float dX = Mathf.Abs(x - iX);
                float dY = Mathf.Abs(y - iY);
                float dZ = Mathf.Abs(-x - y - iZ);

                if (dX > dY && dX > dZ)
                {
                    iX = -iY - iZ;
                }
                else if (dZ > dY)
                {
                    iZ = -iX - iY;
                }
            }

            #endregion

            cubeX = iX;
            cubeY = iY;
            cubeZ = iZ;
        }

        public HexCell(Vector3 position, GameObject node)
        {
            cellPosition = position;

            cellNode = node;

            #region Constructs Cube Coordinates

            float x = position.x / (HexData.innerRadius * 2f);
            float y = -x;

            float offset = position.z / (HexData.outerRadius * 3f);
            x -= offset;
            y -= offset;

            int iX = Mathf.RoundToInt(x);
            int iY = Mathf.RoundToInt(y);
            int iZ = Mathf.RoundToInt(-x - y);

            if (iX + iY + iZ != 0)
            {
                float dX = Mathf.Abs(x - iX);
                float dY = Mathf.Abs(y - iY);
                float dZ = Mathf.Abs(-x - y - iZ);

                if (dX > dY && dX > dZ)
                {
                    iX = -iY - iZ;
                }
                else if (dZ > dY)
                {
                    iZ = -iX - iY;
                }
            }

            #endregion

            cubeX = iX;
            cubeY = iY;
            cubeZ = iZ;
        }

        #endregion

        #region Functions
                
        #endregion
    }

    public static class HexData
    {
        public const float outerRadius = 10f;
        public const float innerRadius = outerRadius * 0.866f;

        public static Vector3[] corners =
        {
        new Vector3(0f,0f,0f),
        new Vector3(0f,0f,outerRadius),
        new Vector3(innerRadius, 0f, 0.5f*outerRadius),
        new Vector3(innerRadius, 0f, -0.5f*outerRadius),
        new Vector3(0f,0f,-outerRadius),
        new Vector3(-innerRadius, 0f, -0.5f*outerRadius),
        new Vector3(-innerRadius, 0f, 0.5f*outerRadius)
        };

        public static Vector2[] UVs =
        {
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 1.0f),
        new Vector2(1.0f, 0.75f),
        new Vector2(1.0f, 0.25f),
        new Vector2(0.5f, 0f),
        new Vector2(0f, 0.25f),
        new Vector2(0f, 0.75f)
        };
    }

    public class Player{

        #region Variables

        public int currentAP = 0;

        public int layer;

        public List<GameObject> nodeCollection = new List<GameObject>();

        public List<GameObject> nodeInCapture = new List<GameObject>();

        public List<GameObject> unitCollection = new List<GameObject>();

        public Material playerMaterial;

        #endregion Variables

        #region Constructors

        public Player(Material material, int startCellElement, HexCell[] cells, Color playerColor, int playerLayer)
        {
            playerMaterial = material;

            playerMaterial.color = playerColor;

            layer = playerLayer;

            nodeCollection.Add(cells[startCellElement].cellNode);

            nodeCollection[0].GetComponent<Renderer>().material = playerMaterial;

            nodeCollection[0].layer = playerLayer;
        }

        #endregion Constructors

        #region Functions

        public void AddToNodeCollection(GameObject node)
        {
            nodeCollection.Add(node);

            node.GetComponent<Node>().owner = this;
        }

        public void AddToNodeInCapture(GameObject node)
        {
            nodeInCapture.Add(node);
            node.GetComponent<Node>().isCurrentlyInCapture = true;
            node.GetComponent<Renderer>().material = playerMaterial;
            node.layer = layer;
        }

        public void AddToUnitCollection(GameObject unit)
        {
            unitCollection.Add(unit);
            //unit.GetComponent<Renderer>().color = ;
            unit.layer = layer;

            unit.GetComponent<Unit>().owner = this;
        }

        public void CalculateActionPointsForTurn()
        {
            for (int i = 0; i < nodeCollection.Count; i++)
            {
                Node localNode = nodeCollection[i].GetComponent<Node>();

                if(!localNode.isCurrentlyUpgrading)
                    currentAP += localNode.GetTotalAPValue();
            }
        }

        public void CalculateCurrentCaptureStates()
        {
            for (int i = 0; i < nodeInCapture.Count; i++)
            {
                Node localNode = nodeInCapture[i].GetComponent<Node>();

                if (localNode.isCurrentlyInCapture)
                {
                    localNode.currentCaptureIterator--;

                    if (localNode.currentCaptureIterator == 0)
                    {
                        nodeInCapture.Remove(localNode.gameObject);
                        nodeCollection.Add(localNode.gameObject);
                        localNode.isCurrentlyInCapture = false;
                        
                    }

                    else
                        localNode.transform.localScale += Vector3.one * localNode.scaleFactor;
                }
            }
        }

        #endregion Functions
    }

    #endregion Game Classes

    #region Interfaces
    
    #endregion
}