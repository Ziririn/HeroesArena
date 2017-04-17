﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeroesArena
{
    // Represents view of the map.
    public class GameView : MonoBehaviour, IPointerClickHandler
    {
        // Notifications.
        public const string CellClickedNotification = "GameView.CellClickedNotification";
        public const string EndTurnClickedNotification = "GameView.EndTurnClickedNotification";

        // Sizes of tiles.
        public const int CellXPixelSize = 20, CellYPixelSize = 15;

        // Important references.
        public MatchController MatchController;
        public CameraController CameraController;

        #region UI References
        // UI references.
        public GameObject Tiles;
        public GameObject Objects;
        public GameObject Units;
        public GameObject Grid;
        public GameObject ActionHighlights;
        public GameObject SelectedAreaHighlights;
        public GameObject MouseHighlight;
        public Text LocalPlayerLabel;
        public Text GameStateLabel;
        public GameObject HealthBar;
        public GameObject ActionBar;
        public Button EndTurnButton;
        public Button AttackButton;
        public Button MoveButton;
        #endregion

        #region Prefabs
        // TODO maybe it should be moved somewhere else.
        // Links to some prefabs.
        [SerializeField]
        private GameObject GridTile;
        [SerializeField]
        private GameObject Ground;
        [SerializeField]
        private GameObject Ground1;
        [SerializeField]
        private GameObject Ground2;
        [SerializeField]
        private GameObject RedGround1;
        [SerializeField]
        private GameObject RedGround2;
        [SerializeField]
        private GameObject WallLow;
        [SerializeField]
        private GameObject Wall;
        [SerializeField]
        private GameObject Rogue;
        [SerializeField]
        private GameObject Warrior;
        [SerializeField]
        private GameObject Wizard;
        [SerializeField]
        private GameObject HealthPotion;
        [SerializeField]
        private GameObject ActionHighlight;
        #endregion

        // Stores gameobject references and provides easy position-gameobjects access.
        private Dictionary<Coordinates, List<GameObject>> _shownCells;
        // Stores gameobject references and provides easy position-gameobjects access for cells in FOW.
        private Dictionary<Coordinates, List<GameObject>> _shownFOWCells;
        // Stores old map
        private Map _oldMap;
        // Checks if grid should be shown.
        private bool _showGrid = true;
        // Checks if action highlights should be shown.
        private bool _showActionHighlights = false;
        // Action that would be used upon click.
        private ActionTag _clickAction = ActionTag.LongMove;

        // Initialization.
        private void OnEnable()
        {
            _shownCells = new Dictionary<Coordinates, List<GameObject>>();
            _shownFOWCells = new Dictionary<Coordinates, List<GameObject>>();
        }

        // Executed at every frame.
        private void Update()
        {

        }

        #region Show/Clear Map
        // Shows one cell.
        public void Show(Cell cell)
        {
            // New list to be stored in shownCells later.
            List<GameObject> gameObjects = new List<GameObject>();

            // TODO tile should depend on cell.Tile
            // Shows tile of the cell.
            GameObject tile = ShowOnGrid(GetTilePrefab(cell.Tile.Type), Tiles, cell);
            gameObjects.Add(tile);

            // TODO unit should depend on cell.Unit
            // Shows unit on the cell.
            if (cell.Unit != null)
            {
                GameObject unit = ShowOnGrid(GetUnitPrefab(cell.Unit.Class.Tag), Units, cell);
                gameObjects.Add(unit);

                // Updates unit animation.
                UpdateUnitAnimation(cell.Unit, unit);

                // Fill health bar.
                Parameter<int> healthPoints = cell.Unit.HealthPoints;
                FillBar(unit.transform.Find("HealthBar"), healthPoints);

                // Fill action bar.
                Parameter<int> actionPoints = cell.Unit.ActionPoints;
                FillBar(unit.transform.Find("ActionBar"), actionPoints);

                // Sets camera to follow unit if it is controlled by local player. Also fills bars on UI.
                if (cell.Unit == MatchController.LocalPlayer.ControlledUnit)
                {
                    FillBar(HealthBar.transform, healthPoints);
                    FillBar(ActionBar.transform, actionPoints);

                    CameraController.Target = unit.transform;
                }
            }

            // TODO object should depend on cell.Object
            // Shows object on the cell.
            if (cell.Object != null)
            {
                GameObject obj = ShowOnGrid(HealthPotion, Objects, cell);
                gameObjects.Add(obj);
            }

            // Shows one grid cell if it is to be shown.
            if (_showGrid)
                ShowGridCell(cell.Position);

            // Remembers new gameObjects as shown.
            _shownCells[cell.Position] = gameObjects;
        }

        // Shows sent map.
        public void Show(Map map)
        {
            // Clears mismatches between new and old maps.
            ClearMismatch(map);
            foreach (Cell cell in map.Cells.Values)
            {
                // Shows cell if nothing is shown at the position.
                if (!_shownCells.ContainsKey(cell.Position))
                {
                    if (_shownFOWCells.ContainsKey(cell.Position))
                    {
                        foreach (GameObject obj in _shownFOWCells[cell.Position])
                        {
                            Destroy(obj);
                        }
                        _shownFOWCells.Remove(cell.Position);
                    }
                    Show(cell);
                }
            }
            _oldMap = (Map)map.Clone();
            if (_showActionHighlights)
                ShowActionHighlights();
        }

        // Clears everything that is not supposed to be shown.
        public void ClearMismatch(Map map)
        {
            if (_oldMap != null)
            {
                // Remembers coordinates to clear later.
                List<Coordinates> keysToDelete = new List<Coordinates>();
                List<Coordinates> keysToTurnFOW = new List<Coordinates>();
                foreach (Coordinates pos in _shownCells.Keys)
                {
                    // Clears gameObjects for position if it is not part of new map or if it differs from new map.
                    if (!map.Cells.ContainsKey(pos))
                        keysToTurnFOW.Add(pos);
                    else if (!_oldMap.Cells[pos].Equals(map.Cells[pos]))
                        keysToDelete.Add(pos);
                }
                // Clears the unneeded coordinates from shownCells.
                foreach (Coordinates pos in keysToDelete)
                {
                    foreach (GameObject obj in _shownCells[pos])
                        Destroy(obj);
                    _shownCells.Remove(pos);
                }
                foreach (Coordinates pos in keysToTurnFOW)
                {
                    _shownFOWCells[pos] = _shownCells[pos];
                    _shownCells.Remove(pos);
                    foreach (GameObject obj in _shownFOWCells[pos])
                    {
                        if (obj.CompareTag("Unit") || obj.CompareTag("Object"))
                            Destroy(obj);
                        else
                            obj.GetComponent<SpriteRenderer>().color = new Color(0.3f, 0.3f, 0.3f);
                    }
                }
            }
        }

        // Clears the entire map.
        public void Clear()
        {
            DestroyAllChildren(Tiles.transform);
            DestroyAllChildren(Objects.transform);
            DestroyAllChildren(Units.transform);
            ClearGrid();
            ClearActionHighlights();
            MouseHighlight.SetActive(false);
            _shownCells = new Dictionary<Coordinates, List<GameObject>>();
            _oldMap = null;
        }
        #endregion

        public void ShowMouseHighlight(Coordinates target)
        {
            if (_shownCells.ContainsKey(target))
            {
                MouseHighlight.SetActive(true);
                SetOnGrid(MouseHighlight, target);
            }
            else
                MouseHighlight.SetActive(false);
        }

        #region Show/Clear Grid
        // Shows one grid cell.
        public void ShowGridCell(Coordinates pos)
        {
            ShowOnGrid(GridTile, Grid, pos);
        }
        // Clears and then shows full grid.
        public void ShowGrid()
        {
            ClearGrid();
            _showGrid = true;
            foreach (Coordinates pos in _shownCells.Keys)
                ShowGridCell(pos);
            foreach (Coordinates pos in _shownFOWCells.Keys)
                ShowGridCell(pos);
        }
        // Hides grid.
        public void HideGrid()
        {
            _showGrid = false;
            ClearGrid();
        }
        // Clears grid.
        public void ClearGrid()
        {
            DestroyAllChildren(Grid);
        }
        #endregion

        #region Show/Clear Selected Area Highlights
        // Shows one selected area highlight cell.
        public void ShowSelectedAreaHighlightCell(Coordinates pos)
        {
            ShowOnGrid(ActionHighlight, SelectedAreaHighlights, pos);
        }
        // Clears and then shows selected area highlights.
        public void ShowSelectedAreaHighlights(Coordinates target)
        {
            MouseHighlight.SetActive(true);
            ClearSelectedAreaHighlights();
            Action action = MatchController.LocalPlayer.ControlledUnit.Actions[_clickAction];
            foreach (Cell cell in action.SelectedArea(target, _oldMap))
            {
                ShowSelectedAreaHighlightCell(cell.Position);
            }
        }
        // Clears selected area highlights.
        public void ClearSelectedAreaHighlights()
        {
            DestroyAllChildren(SelectedAreaHighlights);
        }
        #endregion

        #region Show/Clear Action Highlights
        // Shows one action highlight cell.
        public void ShowActionHighlightCell(Coordinates pos)
        {
            ShowOnGrid(ActionHighlight, ActionHighlights, pos);
        }
        // Clears and then shows action highlights.
        public void ShowActionHighlights()
        {
            ClearActionHighlights();
            _showActionHighlights = true;
            Action action = MatchController.LocalPlayer.ControlledUnit.Actions[_clickAction];
            if (action == null)
                return;
            List<Cell> cells = action.AllInRange(_oldMap);
            foreach (Cell cell in cells)
            {
                ShowActionHighlightCell(cell.Position);
            }
        }
        // Hides action highlights.
        public void HideActionHighlights()
        {
            _showActionHighlights = false;
            ClearActionHighlights();
        }
        // Clears action highlights.
        public void ClearActionHighlights()
        {
            DestroyAllChildren(ActionHighlights);
        }
        #endregion

        #region Player Actions
        // Called when map cell is clicked.
        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            // Gets click position.
            Coordinates clickPos = WorldToCoordinates(eventData.pointerCurrentRaycast.worldPosition);

            // Notifies GameController.ActiveGameState that cell was clicked.
            this.PostNotification(CellClickedNotification, new ActionParameters(_clickAction, clickPos));
        }

        // Called when EndTurn button is clicked.
        public void OnEndTurnClick()
        {
            // Sets chosen click action back to move.
            OnMoveClick();

            // Notifies GameController.ActiveGameState that EndTurn button was clicked.
            this.PostNotification(EndTurnClickedNotification);
        }

        // Called when Move button is clicked.
        public void OnMoveClick()
        {
            // Sets correct click action.
            _clickAction = ActionTag.LongMove;

            // Sets button not interactable anymore.
            MoveButton.interactable = false;
            AttackButton.interactable = true;

            ShowActionHighlights();
        }

        // Called when Attack button is clicked.
        public void OnAttackClick()
        {
            // Sets correct click action.
            _clickAction = ActionTag.Attack;

            // Sets button not interactable anymore.
            AttackButton.interactable = false;
            MoveButton.interactable = true;
            ShowActionHighlights();
        }
        #endregion

        #region Static Grid Methods
        // Gets grid coordinates from world position.
        public static Coordinates WorldToCoordinates(Vector2 pos)
        {
            int x = Mathf.RoundToInt(pos.x / CellXPixelSize);
            int y = Mathf.RoundToInt((pos.y - CellYPixelSize / 2f) / CellYPixelSize);
            return new Coordinates(x, y);
        }

        // Sets position on grid for object.
        public static void SetGridPosition(GameObject obj, Coordinates coords)
        {
            obj.transform.localPosition = new Vector2(coords.X * CellXPixelSize, coords.Y * CellYPixelSize);
        }

        // Sets sorting order for object.
        public static void SetSortingOrder(GameObject obj, int order)
        {
            obj.GetComponent<SpriteRenderer>().sortingOrder = order;
        }

        // Sets position on grid and correct sorting order for object.
        public static void SetOnGrid(GameObject gameObj, Coordinates pos)
        {
            SetGridPosition(gameObj, pos);
            SetSortingOrder(gameObj, -pos.Y);
        }
        public static void SetOnGrid(GameObject gameObj, Cell cell)
        {
            SetOnGrid(gameObj, cell.Position);
        }

        // Shows one prefab object on grid.
        public static GameObject ShowOnGrid(GameObject prefab, GameObject parent, Coordinates pos)
        {
            GameObject obj = Instantiate(prefab, parent.transform);
            SetOnGrid(obj, pos);
            return obj;
        }
        public static GameObject ShowOnGrid(GameObject prefab, GameObject parent, Cell cell)
        {
            return ShowOnGrid(prefab, parent, cell.Position);
        }
        #endregion

        // Fills the given bar with given int parameter.
        public static void FillBar(Transform bar, Parameter<int> par)
        {
            bar.Find("Fill").GetComponent<Image>().fillAmount = (float)par.Current / par.Maximum;
        }

        // TODO do here more than just direction change.
        // Updates units animation.
        public static void UpdateUnitAnimation(BasicUnit unit, GameObject unitObject)
        {
            // Gets unit animator.
            Animator animator = unitObject.GetComponent<Animator>();

            if (animator == null)
                return;

            // Sets animation depending on facing.
            switch (unit.Facing)
            {
                case Direction.Up:
                    animator.SetFloat("xFacing", 0);
                    animator.SetFloat("yFacing", 1);
                    break;
                case Direction.Left:
                    animator.SetFloat("xFacing", -1);
                    animator.SetFloat("yFacing", 0);
                    break;
                case Direction.Down:
                    animator.SetFloat("xFacing", 0);
                    animator.SetFloat("yFacing", -1);
                    break;
                case Direction.Right:
                    animator.SetFloat("xFacing", 1);
                    animator.SetFloat("yFacing", 0);
                    break;
            }
        }

        // Returns a corresponding tile prefab.
        public GameObject GetTilePrefab(TileType type)
        {
            // TODO rewrite the whole tile system
            switch (type)
            {
                case TileType.Ground:
                    return Ground;
                case TileType.Ground1:
                    return Ground1;
                case TileType.Ground2:
                    return Ground2;
                case TileType.RedGround1:
                    return RedGround1;
                case TileType.RedGround2:
                    return RedGround2;
                case TileType.Wall:
                    return Wall;
                case TileType.WallLow:
                    return WallLow;
            }

            return null;
        }

        // Returns a corresponding unit prefab.
        public GameObject GetUnitPrefab(ClassTag clas)
        {
            switch (clas)
            {
                case ClassTag.Rogue:
                    return Rogue;
                case ClassTag.Wizard:
                    return Wizard;
                case ClassTag.Warrior:
                    return Warrior;
                default:
                    return Rogue;
            }
        }

        #region Destroy Children
        // TODO should not be here.
        // Destroys all children of transform.
        public static void DestroyAllChildren(Transform obj)
        {
            int count = obj.childCount;
            for (int j = count - 1; j >= 0; --j)
                Destroy(obj.GetChild(j).gameObject);
        }
        public static void DestroyAllChildren(GameObject obj)
        {
            DestroyAllChildren(obj.transform);
        }
        #endregion
    }
}
