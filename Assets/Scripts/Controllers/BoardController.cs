using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;

public class BoardController : MonoBehaviour, IAutoplayBoard
{
    public event Action OnMoveEvent = delegate { };

    public bool IsBusy { get; private set; }

    private Board m_board;

    private GameManager m_gameManager;
    private BottomManager m_bottomManager;


    private bool m_isDragging;

    private Camera m_cam;

    private Collider2D m_hitCollider;

    private GameSettings m_gameSettings;

    private List<Cell> m_potentialMatch;

    private float m_timeAfterFill;

    private bool m_hintIsShown;

    private bool m_gameOver;
    private bool m_gameComplete;

    public void StartGame(GameManager gameManager, GameSettings gameSettings)
    {
        m_gameManager = gameManager;

        m_gameSettings = gameSettings;

        m_gameManager.StateChangedAction += OnGameStateChange;

        m_cam = Camera.main;

        m_board = new Board(this.transform, gameSettings);
        m_bottomManager = FindObjectOfType<BottomManager>();
        if (m_bottomManager == null)
        {
            GameObject go = new GameObject("BottomManager");
            go.transform.position = new Vector3(-2f, -4.5f, 0);
            m_bottomManager = go.AddComponent<BottomManager>();
            // init with capacity from settings
            int capacity = (gameSettings != null && gameSettings.BottomSlotsCount > 0) ? gameSettings.BottomSlotsCount : 5;
            m_bottomManager.Init(capacity);
            CreateBottomBackgroundSlots();
        }
        Fill();
    }
    private void CreateBottomBackgroundSlots()
    {
        for (int i = 0; i < m_bottomManager.Capacity; i++)
        {
            // Tạo cell cho bottom manager
            GameObject cellGO = new GameObject($"BottomCell_{i}");
            cellGO.transform.SetParent(m_bottomManager.transform, false);
            cellGO.transform.localPosition = new Vector3(i * 1f, 0f, 0f);

            // Thêm component Cell
            Cell cell = cellGO.AddComponent<Cell>();

            // Thêm collider để click
            BoxCollider2D collider = cellGO.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 0.8f);

            // Thêm background visual (tùy chọn)
            GameObject prefabBG = Resources.Load<GameObject>(Constants.PREFAB_CELL_BACKGROUND);
            if (prefabBG != null)
            {
                GameObject bg = Instantiate(prefabBG);
                bg.transform.SetParent(cellGO.transform, false);
                bg.transform.localPosition = Vector3.zero;
                // Xóa collider từ background để không cản trở
                Collider2D bgCollider = bg.GetComponent<Collider2D>();
                if (bgCollider != null) Destroy(bgCollider);
            }
        }
    }


    private void Fill()
    {
        m_board.Fill();
        // FindMatchesAndCollapse();
    }

    private void OnGameStateChange(GameManager.eStateGame state)
    {
        switch (state)
        {
            case GameManager.eStateGame.GAME_STARTED:
                IsBusy = false;
                break;
            case GameManager.eStateGame.PAUSE:
                IsBusy = true;
                break;
            case GameManager.eStateGame.GAME_OVER:
                m_gameOver = true;
                StopHints();
                break;
            case GameManager.eStateGame.GAME_COMPLETE:
                m_gameComplete = true;
                break;

        }
    }


    public void Update()
    {
        if (m_gameOver || m_gameComplete || IsBusy) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 worldPoint = m_cam.ScreenToWorldPoint(Input.mousePosition);

            // Dùng OverlapPoint thay vì Raycast cho click 2D
            Collider2D hitCollider = Physics2D.OverlapPoint(worldPoint);
            if (hitCollider != null)
            {
                GameObject clickedObj = hitCollider.gameObject;

                // 1. Click vào item trong BottomManager
                if (clickedObj.transform.IsChildOf(m_bottomManager.transform))
                {
                    HandleBottomManagerClick(clickedObj);
                    return; // tránh click đồng thời vào Cell
                }

                // 2. Click vào Cell trên board
                Cell cell = clickedObj.GetComponent<Cell>();
                if (cell != null && !cell.IsEmpty)
                {
                    OnTapCell(cell);
                }
            }
        }
    }
    private void HandleBottomManagerClick(GameObject clickedObj)
    {
        // Tìm item trong BottomManager dựa trên view
        Item item = m_bottomManager.GetItemByView(clickedObj);
        if (item != null && item.OriginalCell != null)
        {
            ReturnItemToBoard(item);
        }
        else
        {
            // Nếu không tìm thấy item trực tiếp, có thể click vào cell trong bottom manager
            Cell bottomCell = clickedObj.GetComponent<Cell>();
            if (bottomCell != null && !bottomCell.IsEmpty)
            {
                ReturnItemToBoard(bottomCell.Item);
            }
        }
    }
    private void ReturnItemToBoard(Item item)
    {
        if (IsBusy) return;
        if (item == null || item.OriginalCell == null) return;

        // Kiểm tra ô gốc có trống không
        if (!item.OriginalCell.IsEmpty)
        {
            Debug.Log("Original cell is not empty, cannot return item");
            return;
        }

        IsBusy = true;

        // Remove khỏi BottomManager
        bool removed = m_bottomManager.RemoveItem(item);
        if (!removed)
        {
            IsBusy = false;
            return;
        }

        // Trả item về ô gốc
        item.OriginalCell.SetItem(item);

        // Di chuyển item về vị trí cell gốc
        if (item.View != null)
        {
            item.View.transform.SetParent(item.OriginalCell.transform, true);
            item.MoveToPosition(item.OriginalCell.transform.position, 0.3f, () =>
            {
                IsBusy = false;
                Debug.Log("Item returned to board");
            });
        }
        else
        {
            IsBusy = false;
        }
    }


    private void OnTapCell(Cell cell)
    {
        if (IsBusy) return;
        if (cell == null || cell.IsEmpty) return;

        // Nếu cell này thuộc về BottomManager, xử lý riêng
        if (cell.transform.IsChildOf(m_bottomManager.transform))
        {
            if (!cell.IsEmpty)
            {
                ReturnItemToBoard(cell.Item);
            }
            return;
        }

        // Nếu click vào cell trên Board
        if (m_bottomManager.IsFull())
        {
            m_gameManager.GameOver();
            return;
        }

        MoveItemToBottom(cell);
    }
    private void MoveItemToBottom(Cell boardCell)
    {
        IsBusy = true;
        Item item = boardCell.Item;

        // Lưu reference đến cell gốc TRƯỚC KHI free
        item.OriginalCell = boardCell;

        boardCell.Free();

        if (item.View != null)
        {
            item.View.transform.SetParent(m_bottomManager.transform, true);
        }

        bool success = m_bottomManager.TryAddItemToBottom(item, 0.35f, () =>
        {
            OnMoveEvent?.Invoke();
            StartCoroutine(AfterBottomUpdate());
        });

        if (!success)
        {
            IsBusy = false;
            m_gameManager.GameOver();
            return;
        }
    }
    private IEnumerator AfterBottomUpdate()
    {
        yield return new WaitForSeconds(0.4f);

        // Check lose
        if (m_bottomManager.IsFull())
        {
            Debug.Log("Is full");
            if (!m_board.IsEmpty())
            {
                IsBusy = false;
                Debug.Log("Not empty");
                m_gameManager.GameOver();
                yield break;
            }
        }

        // Check win
        if (m_board.IsEmpty())
        {
            IsBusy = false;
            m_gameManager.GameComplete();
            yield break;
        }

        IsBusy = false;
    }

    private void ResetRayCast()
    {
        m_isDragging = false;
        m_hitCollider = null;
    }



    private List<Cell> GetMatches(Cell cell)
    {
        List<Cell> listHor = m_board.GetHorizontalMatches(cell);
        if (listHor.Count < m_gameSettings.MatchesMin)
        {
            listHor.Clear();
        }

        List<Cell> listVert = m_board.GetVerticalMatches(cell);
        if (listVert.Count < m_gameSettings.MatchesMin)
        {
            listVert.Clear();
        }

        return listHor.Concat(listVert).Distinct().ToList();
    }

    private void CollapseMatches(List<Cell> matches, Cell cellEnd)
    {
        for (int i = 0; i < matches.Count; i++)
        {
            matches[i].ExplodeItem();
        }

        if (matches.Count > m_gameSettings.MatchesMin)
        {
            m_board.ConvertNormalToBonus(matches, cellEnd);
        }

        StartCoroutine(ShiftDownItemsCoroutine());
    }

    private IEnumerator ShiftDownItemsCoroutine()
    {
        m_board.ShiftDownItems();

        yield return new WaitForSeconds(0.2f);

        m_board.FillGapsWithNewItems();

        yield return new WaitForSeconds(0.2f);

        // FindMatchesAndCollapse();
    }

    private IEnumerator RefillBoardCoroutine()
    {
        m_board.ExplodeAllItems();

        yield return new WaitForSeconds(0.2f);

        m_board.Fill();

        yield return new WaitForSeconds(0.2f);

        // FindMatchesAndCollapse();
    }

    private IEnumerator ShuffleBoardCoroutine()
    {
        m_board.Shuffle();

        yield return new WaitForSeconds(0.3f);

        // FindMatchesAndCollapse();
    }


    private void SetSortingLayer(Cell cell1, Cell cell2)
    {
        if (cell1.Item != null) cell1.Item.SetSortingLayerHigher();
        if (cell2.Item != null) cell2.Item.SetSortingLayerLower();
    }

    private bool AreItemsNeighbor(Cell cell1, Cell cell2)
    {
        return cell1.IsNeighbour(cell2);
    }

    internal void Clear()
    {
        if (m_board != null)
        {
            m_board.Clear();
            m_board = null;
        }

        if (m_bottomManager != null)
        {
            m_bottomManager.ClearAll();
        }
    }

    // private void ShowHint()
    // {
    //     m_hintIsShown = true;
    //     var randomCells = GetRandomNonEmptyCells(3);
    //     foreach (var cell in randomCells)
    //     {
    //         cell.AnimateItemForHint();
    //     }
    // }

    private void StopHints()
    {
        m_hintIsShown = false;
        // foreach (var cell in m_potentialMatch)
        // {
        //     cell.StopHintAnimation();
        // }

        // m_potentialMatch.Clear();
    }

    public bool TryFindWinningMove(out Cell cell1, out Cell cell2)
    {
        cell1 = null;
        cell2 = null;

        List<Cell> availableCells = GetAllCells();

        if (availableCells.Count == 0)
            return false;

        // Nếu còn needed item
        if (m_bottomManager.GetNeededItemTypes(out List<string> neededTypes) && neededTypes.Count > 0)
        {
            Cell winningCell = availableCells
                .FirstOrDefault(c => c.Item != null && neededTypes.Contains(GetItemKey(c.Item)));

            if (winningCell != null)
            {
                cell1 = winningCell;
                Cell tempCell1 = cell1;

                cell2 = availableCells.FirstOrDefault(c => c != tempCell1 && tempCell1.IsNeighbour(c))
                        ?? availableCells.FirstOrDefault(c => c != tempCell1);

                return true;
            }
        }

        // Trường hợp còn 1 ô để win nhưng m_bottomManager full
        if (!m_board.IsEmpty() && m_bottomManager.CountFilled() < m_bottomManager.Capacity)
        {
            cell1 = availableCells[0];
            cell2 = null;
            return true;
        }

        // fallback safe move
        return TryFindSafeMove(out cell1, out cell2);
    }


    public bool TryFindLosingMove(out Cell cell1, out Cell cell2)
    {
        cell1 = null;
        cell2 = null;

        // Lấy tất cả các cell còn item
        List<Cell> availableCells = GetAllCells();
        if (availableCells.Count == 0)
            return false;

        cell1 = availableCells[0];
        cell2 = null;
        return true;
    }



    public IEnumerator SwapAndProcess(Cell cell1, Cell cell2)
    {
        if (cell1 == null || cell1.IsEmpty)
            yield break;

        IsBusy = true;

        Item item = cell1.Item;
        cell1.Free();

        if (item.View != null)
            item.View.transform.SetParent(m_bottomManager.transform, true);

        bool success = m_bottomManager.TryAddItemToBottom(item, 0.35f, () =>
        {
            OnMoveEvent?.Invoke();
        });

        yield return new WaitForSeconds(0.5f);

        // Nếu game over xảy ra (autolose) hoặc board empty (autowin)

        IsBusy = false;
    }





    public List<Cell> GetAllCells()
    {

        if (m_board != null)
        {
            return m_board.GetAllCells()?.Where(c => c != null && !c.IsEmpty).ToList() ?? new List<Cell>();
        }
        return new List<Cell>();
    }
    private string GetItemKey(Item item)
    {
        if (item is NormalItem n)
        {
            return n.ItemType.ToString();
        }
        return item.GetType().Name;
    }
    private bool TryFindSafeMove(out Cell cell1, out Cell cell2)
    {
        cell1 = null;
        cell2 = null;

        List<Cell> availableCells = GetAllCells();
        if (availableCells.Count == 0) return false;

        cell1 = availableCells[0];
        cell2 = availableCells.Count > 1 ? availableCells[1] : null; // cell2 luôn != null

        return true;
    }
    public bool CheckWinCondition()
    {
        if (m_board != null && m_board.IsEmpty())
        {
            if (!m_gameComplete)
            {
                IsBusy = false;
                m_gameComplete = true;
                m_gameManager.GameComplete();
                Debug.Log("BoardController: GameComplete triggered!");
            }
            return true;
        }
        return false;
    }
    public bool CheckLose()
    {
        if (m_bottomManager.IsFull())
        {
            Debug.Log("Is full");
            if (!m_board.IsEmpty())
            {
                IsBusy = false;
                Debug.Log("Not empty");
                m_gameManager.GameOver();
            }
            return true;
        }
        return false;
    }
}
