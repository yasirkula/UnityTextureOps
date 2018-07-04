using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent( typeof( RectTransform ), typeof( RawImage ), typeof( Button ) )]
public class SlidePuzzlePiece : MonoBehaviour
{
	private RectTransform rectTransform;

	private int row, column;
	private int initialRow, initialColumn;
	public bool IsInCorrectPosition { get { return row == initialRow && column == initialColumn; } }

	private void Awake()
	{
		rectTransform = GetComponent<RectTransform>();
	}

	private void OnDestroy()
	{
		// A procedural texture must be destroyed manually to free up memory
		Destroy( GetComponent<RawImage>().texture );
	}

	public void InitializeComponents( Texture texture, UnityAction onClick )
	{
		rectTransform.anchorMin = new Vector2( 0f, 0f );
		rectTransform.anchorMax = new Vector2( 1f, 1f );
		rectTransform.sizeDelta = new Vector2( 0f, 0f );
		rectTransform.anchoredPosition = new Vector2( 0f, 0f );

		GetComponent<RawImage>().texture = texture;
		GetComponent<Button>().onClick.AddListener( onClick );
	}

	public void InitializePosition( int row, int column, int initialRow, int initialColumn )
	{
		this.row = row;
		this.column = column;
		this.initialRow = initialRow;
		this.initialColumn = initialColumn;
	}

	public bool IsAdjacentTo( SlidePuzzlePiece otherPiece )
	{
		return ( row == otherPiece.row && Mathf.Abs( column - otherPiece.column ) == 1 ) ||
			( column == otherPiece.column && Mathf.Abs( row - otherPiece.row ) == 1 );
	}

	public void SwapPlacesWith( SlidePuzzlePiece otherPiece )
	{
		int row = this.row;
		int column = this.column;
		Transform parent = transform.parent;

		this.row = otherPiece.row;
		this.column = otherPiece.column;
		otherPiece.row = row;
		otherPiece.column = column;

		transform.SetParent( otherPiece.transform.parent, false );
		otherPiece.transform.SetParent( parent, false );
	}
}