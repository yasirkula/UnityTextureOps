using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class SlidePuzzleManager : MonoBehaviour
{
#pragma warning disable 0649
	[SerializeField]
	private AspectRatioFitter slotsParent;

	[SerializeField]
	private int puzzleSize = 3;
	private int rowCount, columnCount;

	[SerializeField]
	private int shuffleAmount = 1000;

	[SerializeField]
	private Texture puzzleTexture;
#pragma warning restore 0649

	private RectTransform[][] slots; // Slots hold pieces
	private SlidePuzzlePiece[][] pieces;
	private SlidePuzzlePiece blankPiece;

	private void Awake()
	{
		if( puzzleTexture == null || slotsParent == null || puzzleTexture.width < puzzleSize || puzzleTexture.height < puzzleSize || puzzleSize <= 1 )
		{
			Debug.LogError( "Invalid parameter(s)!" );
			Destroy( this );

			return;
		}

		// Create pieces
		CreatePieces();

		// Shuffle pieces
		int shuffleAttempt = 0;
		do
		{
			ShufflePieces();
			shuffleAttempt++;
		} while( IsPuzzleSolved() && shuffleAttempt < 10 ); // For some reason, pieces didn't get shuffled after 10 attempts, avoid infinite loop
	}

	private void CreatePieces()
	{
		// Initialize variables
		int sliceSize = Mathf.Min( puzzleTexture.width / puzzleSize, puzzleTexture.height / puzzleSize );
		rowCount = puzzleTexture.height / sliceSize;
		columnCount = puzzleTexture.width / sliceSize;

		slots = new RectTransform[rowCount][];
		pieces = new SlidePuzzlePiece[rowCount][];

		for( int i = 0; i < rowCount; i++ )
		{
			slots[i] = new RectTransform[columnCount];
			pieces[i] = new SlidePuzzlePiece[columnCount];
		}

		slotsParent.aspectRatio = columnCount / (float) rowCount;

		// Slice puzzleTexture (starts from top-left corner)
		Texture2D[] slices = TextureOps.Slice( puzzleTexture, sliceSize, sliceSize, options: new TextureOps.Options( false, false, true ) );
		for( int i = 0; i < slices.Length; i++ )
			slices[i].wrapMode = TextureWrapMode.Clamp; // To avoid artifacts at edges

		// Create puzzle pieces
		float _1OverRowCount = 1f / rowCount;
		float _1OverColumnCount = 1f / columnCount;
		for( int i = 0, sliceIndex = 0; i < rowCount; i++ ) // Row, starting from top
		{
			for( int j = 0; j < columnCount; j++, sliceIndex++ ) // Column, starting from left
			{
				RectTransform slot = new GameObject( "Slot" + i + "x" + j ).AddComponent<RectTransform>();
				slot.SetParent( slotsParent.transform, false );
				slot.anchorMin = new Vector2( _1OverColumnCount * j, 1f - _1OverRowCount * ( i + 1 ) );
				slot.anchorMax = new Vector2( _1OverColumnCount * ( j + 1 ), 1f - _1OverRowCount * i );
				slot.sizeDelta = new Vector2( 0f, 0f );
				slot.anchoredPosition = new Vector2( 0f, 0f );

				SlidePuzzlePiece piece = new GameObject( "Piece" ).AddComponent<SlidePuzzlePiece>();
				piece.InitializeComponents( slices[sliceIndex], () => OnPieceClicked( piece ) );

				slots[i][j] = slot;
				pieces[i][j] = piece;
			}
		}
	}

	private void ShufflePieces()
	{
		Stopwatch timer = Stopwatch.StartNew();

		// Make the bottom right piece the blank one
		int blankPieceRow = rowCount - 1;
		int blankPieceColumn = columnCount - 1;

		blankPiece = pieces[blankPieceRow][blankPieceColumn];
		blankPiece.gameObject.SetActive( false );

		// Store current parents of the pieces
		int[][] piecesShuffled = new int[rowCount][];
		for( int i = 0; i < rowCount; i++ )
		{
			piecesShuffled[i] = new int[columnCount];
			for( int j = 0; j < columnCount; j++ )
				piecesShuffled[i][j] = i * columnCount + j;
		}

		// Shuffle the pieces
		for( int i = 0; i < shuffleAmount; i++ )
		{
			int movedPieceRow, movedPieceColumn;
			if( Random.value < 0.5f ) // Move a piece horizontally
			{
				movedPieceRow = blankPieceRow;

				if( blankPieceColumn == 0 )
					movedPieceColumn = 1;
				else if( blankPieceColumn == columnCount - 1 )
					movedPieceColumn = blankPieceColumn - 1;
				else
					movedPieceColumn = blankPieceColumn + ( Random.Range( 0, 2 ) * 2 - 1 );
			}
			else // Move a piece vertically
			{
				movedPieceColumn = blankPieceColumn;

				if( blankPieceRow == 0 )
					movedPieceRow = 1;
				else if( blankPieceRow == rowCount - 1 )
					movedPieceRow = blankPieceRow - 1;
				else
					movedPieceRow = blankPieceRow + ( Random.Range( 0, 2 ) * 2 - 1 );
			}

			// Swap array elements
			int temp = piecesShuffled[blankPieceRow][blankPieceColumn];
			piecesShuffled[blankPieceRow][blankPieceColumn] = piecesShuffled[movedPieceRow][movedPieceColumn];
			piecesShuffled[movedPieceRow][movedPieceColumn] = temp;

			blankPieceRow = movedPieceRow;
			blankPieceColumn = movedPieceColumn;
		}

		// Assign the pieces to their shuffled parents
		for( int i = 0; i < rowCount; i++ )
		{
			for( int j = 0; j < columnCount; j++ )
			{
				int targetPieceRow = piecesShuffled[i][j] / columnCount;
				int targetPieceColumn = piecesShuffled[i][j] % columnCount;

				pieces[targetPieceRow][targetPieceColumn].transform.SetParent( slots[i][j], false );
				pieces[targetPieceRow][targetPieceColumn].InitializePosition( i, j, targetPieceRow, targetPieceColumn );
			}
		}

		Debug.Log( "Shuffled " + shuffleAmount + " times in " + timer.ElapsedMilliseconds + " milliseconds" );
	}

	private bool IsPuzzleSolved()
	{
		for( int i = 0; i < rowCount; i++ )
		{
			for( int j = 0; j < columnCount; j++ )
			{
				if( !pieces[i][j].IsInCorrectPosition )
					return false;
			}
		}

		return true;
	}

	private void OnPieceClicked( SlidePuzzlePiece piece )
	{
		// Check if clicked piece is adjacent to the blank piece
		if( piece.IsAdjacentTo( blankPiece ) )
		{
			piece.SwapPlacesWith( blankPiece );

			if( IsPuzzleSolved() )
				Debug.Log( "PUZZLE SOLVED!" );
		}
	}
}