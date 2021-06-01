using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Mach3Engine : MonoBehaviour
{
    [SerializeField] private NotUsable _notUsablePrefab;
    [SerializeField] private Piece _piecePrefab;

    [SerializeField] private Text _scoreText;
    [SerializeField] private Text _mixingText;

    // можно поменять рамеры поля и количество неиспользуемых ячеек
    [SerializeField] private int _gridWidth = 6;
    [SerializeField] private int _gridHeight = 6;
    [SerializeField] private int _countNonUsable = 3;

    private Piece[,] _gridPieces;
    private bool[,] _gridNotUsable;
    private bool[,] _gridToDelete;

    private long _gameScore;

    private float _camHalfWidth;
    private float _camHalfHeight;
    private float _onePieceWidth;
    private float _onePieceHeight;
    private Vector3 _pointLeftBottomBoard;
    private Vector3 _pointRightTopBoard;

    private int _clickedX;
    private int _clickedY;
    private int _firstX=-1;
    private int _firstY=-1;
    private int _secondX=-1;
    private int _secondY=-1;

    private int _movingPieces=0;

    private enum GameStates
    {
        Looking,
        Swapping,
        Deswapping,
        Moving,
        Removing,
        Mixing,
        SelectingFirst,
        SelectingSecond
    }
    private GameStates _gameState;

    private void Start()
    {
        _gameState = GameStates.SelectingFirst;
        CalcBoardParameters();
        FillGridNotUsable();
        FillGridPieces();
    }

    private void Update()
    {
        if (_gameState == GameStates.SelectingFirst)
        {
            if (Input.GetMouseButtonUp(0))
            {
                if (TryCalcClickedPiecePlace())
                {
                    _gridPieces[_clickedX, _clickedY].Select();
                    _firstX = _clickedX;
                    _firstY = _clickedY;
                    _gameState = GameStates.SelectingSecond;
                }
            }
        }

        else if (_gameState == GameStates.SelectingSecond)
        {
            if (Input.GetMouseButtonUp(0))
            {
                if (TryCalcClickedPiecePlace())
                {
                    if (_firstX == _clickedX && _firstY == _clickedY)
                    {
                        _gridPieces[_clickedX, _clickedY].Deselect();
                        _firstX = -1;
                        _firstY = -1;
                        _gameState = GameStates.SelectingFirst;
                    }
                    else if (_firstX == _clickedX && Mathf.Abs(_firstY - _clickedY) == 1 ||
                         _firstY == _clickedY && Mathf.Abs(_firstX - _clickedX) == 1)
                    {
                        _gridPieces[_firstX, _firstY].Deselect();
                        _secondX = _clickedX;
                        _secondY = _clickedY;
                        SwapPieces();
                        _gameState = GameStates.Swapping;
                    }
                    else
                    {
                        _gridPieces[_firstX, _firstY].Deselect();
                        _gridPieces[_clickedX, _clickedY].Select();
                        _firstX = _clickedX;
                        _firstY = _clickedY;
                    }
                }
                else
                {
                    _gridPieces[_firstX, _firstY].Deselect();
                    _firstX = -1;
                    _firstY = -1;
                    _gameState = GameStates.SelectingFirst;
                }
            }
        }

        else if (_gameState == GameStates.Swapping)
        {
            if (_movingPieces == 0)
            {
                if (SearchMatches())
                {
                    _firstX = -1;
                    _firstY = -1;
                    _secondX = -1;
                    _secondY = -1;
                    _gameState = GameStates.Removing;
                }
                else
                {
                    SwapPieces();
                    _gameState = GameStates.Deswapping;
                }
            }
        }

        else if (_gameState == GameStates.Deswapping)
        {
            if (_movingPieces == 0)
            {
                _gameState = GameStates.SelectingFirst;
            }
        }

        else if (_gameState == GameStates.Removing)
        {
            RemoveMatches();
            _gameState = GameStates.Moving;
        }

        else if (_gameState == GameStates.Moving)
        {
            if (_movingPieces == 0)
            {
                if (FillFreePlaces() == false)
                {
                    _gameState = GameStates.Looking;
                }
            }
        }

        else if (_gameState == GameStates.Looking)
        {
            if (SearchMatches())
            {
                _gameState = GameStates.Removing;
            }

            else if (SearchPossibles())
            {
                _gameState = GameStates.SelectingFirst;
            }

            else
            {
                _gameState = GameStates.Mixing;
                _mixingText.gameObject.SetActive(true);
            }
        }

        else if (_gameState == GameStates.Mixing)
        {
            if (Input.GetMouseButtonUp(0))
            {
                _mixingText.gameObject.SetActive(false);
                MixGridPieces();
                _gameState = GameStates.Moving;
            }
        }

    }

    private void SwapPieces()
    {
        // меняем фишки местами
        _gridPieces[_firstX, _firstY].Move(GetPieceWorldPosition(_secondX, _secondY));
        _gridPieces[_secondX, _secondY].Move(GetPieceWorldPosition(_firstX, _firstY));
        Piece tmp = _gridPieces[_firstX, _firstY];
        _gridPieces[_firstX, _firstY] = _gridPieces[_secondX, _secondY];
        _gridPieces[_secondX, _secondY] = tmp;
    }

    private void CalcBoardParameters()
    {
        // расчет параметров игровой доски
        // с помощью них можно занять доской только часть экрана
        _camHalfHeight = Camera.main.orthographicSize;
        _camHalfWidth = Camera.main.aspect * _camHalfHeight;
        _pointLeftBottomBoard = new Vector3(-_camHalfWidth, -_camHalfHeight, 0f);
        _pointRightTopBoard = new Vector3(_camHalfWidth, _camHalfHeight, 0f);
        _onePieceWidth = (_pointRightTopBoard.x - _pointLeftBottomBoard.x) / _gridWidth;
        _onePieceHeight = (_pointRightTopBoard.y - _pointLeftBottomBoard.y) / _gridHeight;
    }

    private void FillGridNotUsable()
    {
        // заполнение массива неиспользуемых полей
        _gridNotUsable = new bool[_gridWidth, _gridHeight];
        for (int i = 0; i < _countNonUsable; i++)
        {
            while (true)
            {
                int x = Random.Range(0, _gridWidth);
                int y = Random.Range(0, _gridHeight);
                if (_gridNotUsable[x, y] == false)
                {
                    _gridNotUsable[x, y] = true;
                    Instantiate(_notUsablePrefab, GetPieceWorldPosition(x, y), Quaternion.identity).Init(_onePieceWidth, _onePieceHeight);
                    break;
                }
            }
        }
    }

    private void FillGridPieces()
    {
        // первое заполнение фишками игрового поля
        _gridPieces = new Piece[_gridWidth, _gridHeight];
        _gridToDelete = new bool[_gridWidth, _gridHeight];
                
        while (true)
        {
            // заполняем сетку, сравнивая каждую фишку с двумя соседями
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (IsUsablePlace(x, y))
                    {
                        while (true)
                        {
                            int newPieceType = _piecePrefab.GetRandomType();
                            if (IsEqvivalentTypePiece(newPieceType, x, y, -1, 0) || IsEqvivalentTypePiece(newPieceType, x, y, 0, -1))
                            {
                                continue;
                            }
                            _gridPieces[x, y] = Instantiate(_piecePrefab, GetPieceWorldPosition(x, y), Quaternion.identity);
                            _gridPieces[x, y].Init(newPieceType, _onePieceWidth, _onePieceHeight);
                            _gridPieces[x, y].StartMovingEvent.AddListener(StartMovingOnePiece);
                            _gridPieces[x, y].StopMovingEvent.AddListener(StopMovingOnePiece);
                            break;
                        }
                    }
                }
            }

            // проверка наличия ходов
            if (SearchPossibles())
            {
                break;
            }

            // если нет ходов - обнуляем и переделываем
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if(IsPiece(x, y))
                    {
                        Destroy(_gridPieces[x, y].gameObject);
                        _gridPieces[x, y] = null;
                    }
                }
            }
            continue;
        }
    }

    private void MixGridPieces()
    {
        // перемешивание фиешк на игровом поле
        List<Piece> piecesOnBoard = new List<Piece>();

        // сохраняем фишки в лист и обнуляем сетку
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                if (IsPiece(x, y))
                {
                    piecesOnBoard.Add(_gridPieces[x, y]);
                    _gridPieces[x, y] = null;
                }
            }
        }

        // заново заполняем сетку сверху вниз
        for (int y = _gridHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                if (IsUsablePlace(x, y))
                {
                    if (piecesOnBoard.Count > 0)
                    {
                        int index = Random.Range(0, piecesOnBoard.Count);
                        _gridPieces[x, y] = piecesOnBoard[index];
                        _gridPieces[x, y].Move(GetPieceWorldPosition(x, y));
                        piecesOnBoard.RemoveAt(index);
                    }
                }
            }
        }
    }

    private bool FillFreePlaces()
    {
        // заполняем найденные пустые поля
        bool ret = false;
        for (int y = _gridHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                if (IsUsablePlace(x, y) && _gridPieces[x, y] == null)
                {
                    // если нижнй ряд, то создаем фишку за границей поля и задвигаем в поле
                    if (y == 0)
                    {
                        _gridPieces[x, y] = Instantiate(_piecePrefab, GetPieceWorldPosition(x, y - 1), Quaternion.identity);
                        _gridPieces[x, y].Init(_piecePrefab.GetRandomType(), _onePieceWidth, _onePieceHeight);
                        _gridPieces[x, y].StartMovingEvent.AddListener(StartMovingOnePiece);
                        _gridPieces[x, y].StopMovingEvent.AddListener(StopMovingOnePiece);
                        _gridPieces[x, y].Move(GetPieceWorldPosition(x, y));
                        ret = true;
                        continue;
                    }

                    // если под полем есть фишка, перемещаем её вверх
                    else if(IsPieceNotMoving(x, y - 1))
                    {
                        MovePieceFrom(x, y, 0, -1);
                        ret = true;
                        continue;
                    }

                    // если под пустым полем нет фишки, а сбоку есть и под боковой есть, то она сваливается с боковой в наше пустое место
                    int side = 1;
                    if (Random.Range(0, 2) == 0)
                    {
                        side = -1;
                    }

                    if (IsPieceNotMoving(x + side, y) || IsUsablePlace(x + side, y) == false)
                    {
                        if (IsPieceNotMoving(x + side, y - 1))
                        {
                            MovePieceFrom(x, y, side, -1);
                            ret = true;
                            continue;
                        }
                    }

                    if (IsPieceNotMoving(x - side, y) || IsUsablePlace(x - side, y) == false)
                    {
                        if (IsPieceNotMoving(x - side, y - 1))
                        {
                            MovePieceFrom(x, y, -side, -1);
                            ret = true;
                            continue;
                        }
                    }
                }
            }
        }
        return ret;
    }

    private bool IsPieceNotMoving(int x, int y)
    {
        if (IsPiece(x, y))
        {
            if (_gridPieces[x, y].IsNotMoving())
            {
                return true;
            }
        }
        return false;
    }

    private bool IsPiece(int x, int y)
    {
        if (IsUsablePlace(x, y))
        {
            if (_gridPieces[x, y] == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    private bool IsUsablePlace(int x, int y)
    {
        if (x < 0 || x >= _gridWidth)
        {
            return false;
        }
        if (y < 0 || y >= _gridHeight)
        {
            return false;
        }
        if (_gridNotUsable[x, y])
        {
            return false;
        }
        return true;
    }

    private void MovePieceFrom(int xTo, int yTo, int xFrom, int yFrom)
    {
        _gridPieces[xTo + xFrom, yTo + yFrom].Move(GetPieceWorldPosition(xTo, yTo));
        _gridPieces[xTo, yTo] = _gridPieces[xTo + xFrom, yTo + yFrom];
        _gridPieces[xTo + xFrom, yTo + yFrom] = null;
    }

    private void RemoveMatches()
    {
        // Уничтожаем все фишки на поле, входящие в совпадения
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                if (_gridToDelete[x, y])
                {
                    _gridPieces[x, y].Remove();
                    _gridPieces[x, y] = null;
                    _gridToDelete[x, y] = false;
                }
            }
        }
    }

    private bool SearchMatches()
    {
        // ищем совпадения и добавляем счёт в зависимости от сложности совпадения
        bool ret = false;
        // поиск в строках
        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth - 2; x++)
            {
                if (IsPiece(x, y))
                {
                    int match = SearchMatchHorizontal(x, y);

                    if (match > 2)
                    {
                        for (int i = 0; i < match; i++)
                        {
                            _gridToDelete[x + i, y] = true;
                        }
                        AddToScore(match);
                        ret = true;
                    }
                    x += match - 1;
                }
            }
        }
        // поиск в столбцах
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight-2; y++)
            {
                if (IsPiece(x, y))
                {
                    int match = SearchMatchVertical(x, y);

                    if (match > 2)
                    {
                        for (int i = 0; i < match; i++)
                        {
                            _gridToDelete[x, y + i] = true;
                        }
                        AddToScore(match);
                        ret = true;
                    }
                    y += match - 1;
                }
            }
        }
        return ret;
    }

    private void AddToScore(int match)
    {
        _gameScore += 10 + (match - 3) * 5;
        _scoreText.text = $"{_gameScore}";
    }

    private int SearchMatchHorizontal(int x, int y)
    {
        int type = _gridPieces[x, y].GetPieceType();
        int match = 1;
        for (int i = 1; x + i < _gridWidth; i++)
        {
            if (IsEqvivalentTypePiece(type, x, y, i, 0))
            {
                match++;
            }
            else
            {
                break;
            }
        }
        return match;
    }

    private int SearchMatchVertical(int x, int y)
    {
        int type = _gridPieces[x, y].GetPieceType();
        int match = 1;
        for (int i = 1; y + i < _gridHeight; i++)
        {
            if (IsEqvivalentTypePiece(type, x, y, 0, i))
            {
                match++;
            }
            else
            {
                break;
            }
        }
        return match;
    }

    private bool SearchPossibles()
    {
        // поиск возможных ходов в соответствии с шаблонами
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                if (IsPiece(x, y))
                {
                    if (MatchPossibilityPattern(x, y, 1, 0, -1, 0, new int[,] { { -2, 0 }, { -1, -1 }, { -1, 1 } })) return true;
                    if (MatchPossibilityPattern(x, y, 1, 0, 2, 0, new int[,] { { 3, 0 }, { 2, -1 }, { 2, 1 } })) return true;
                    if (MatchPossibilityPattern(x, y, 2, 0, 1, 0, new int[,] { { 1, -1 }, { 1, 1 } })) return true;
                    if (MatchPossibilityPattern(x, y, 0, 1, 0, -1, new int[,] { { 0, -2 }, { -1, -1 }, { 1, -1 } })) return true;
                    if (MatchPossibilityPattern(x, y, 0, 1, 0, 2, new int[,] { { 0, 3 }, { -1, 2 }, { 1, 2 } })) return true;
                    if (MatchPossibilityPattern(x, y, 0, 2, 0, 1, new int[,] { { -1, 1 }, { 1, 1 } })) return true;
                }
            }
        }
        return false;
    }

    private bool MatchPossibilityPattern(int x, int y, int xNeedEqv, int yNeedEqv, int xNeedUse, int yNeedUse, int[,] whereFind)
    {
        // поиск возможности совпадения по конкретному шаблону
        if (IsPiece(x + xNeedUse, y + yNeedUse))
        {
            if (IsPiece(x + xNeedEqv, y + yNeedEqv))
            {
                int type = _gridPieces[x, y].GetPieceType();
                if (IsEqvivalentTypePiece(type, x, y, xNeedEqv, yNeedEqv))
                {
                    for (int i = 0; i < whereFind.Length / 2; i++)
                    {
                        if (IsPiece(x + whereFind[i, 0], y + whereFind[i, 1]))
                        {
                            if (IsEqvivalentTypePiece(type, x, y, whereFind[i, 0], whereFind[i, 1]))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    private Vector3 GetPieceWorldPosition(int x, int y)
    {
        float xx = -_camHalfWidth + ((float)x + .5f) * _onePieceWidth;
        float yy = -_camHalfHeight + ((float)y + .5f) * _onePieceHeight;

        return new Vector3(xx, yy, 0f);
    }

    private bool IsEqvivalentTypePiece(int type, int currentX, int currentY, int offsetX, int offsetY)
    {
        int otherX = currentX + offsetX;
        int otherY = currentY + offsetY;
        if(IsPiece(otherX, otherY))
        {
            if (type == _gridPieces[otherX, otherY].GetPieceType())
            {
                return true;
            }
        }
        return false;
    }

    private bool TryCalcClickedPiecePlace()
    {
        Vector3 clickPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (clickPosition.x < _pointLeftBottomBoard.x || clickPosition.x > _pointRightTopBoard.x || 
            clickPosition.y < _pointLeftBottomBoard.y || clickPosition.y > _pointRightTopBoard.y )
        {
            return false;
        }
        Vector3 diff = clickPosition - _pointLeftBottomBoard;
        int x = (int)(diff.x / _onePieceWidth);
        int y = (int)(diff.y / _onePieceHeight);
        if (IsPiece(x, y))
        {
            _clickedX = x;
            _clickedY = y;
            return true;
        }
        return false;
    }

    private void StartMovingOnePiece()
    {
        _movingPieces++;
    }

    private void StopMovingOnePiece()
    {
        _movingPieces--;
    }
}
