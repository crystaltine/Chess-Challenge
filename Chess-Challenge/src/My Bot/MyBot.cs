using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 100, 290, 310, 500, 900, 100000 };
    int MAX_DEPTH_PLY = 4;
    public Move Think(Board board, Timer timer)
    {
        int mySide = board.IsWhiteToMove? 1 : -1;

        Move[] allMoves = board.GetLegalMoves();
        Move bestMove = allMoves[0];

        List<Move> forcingMoves = new List<Move>();

        // Look for the highest eval. Eval function
        // evaluates from the perspective of our side, 
        // so we want the highest achievable
        int bestMoveEval = -1000000;

        foreach (Move move in allMoves)
            if(move.IsCapture || move.IsPromotion /*add checks*/)
                forcingMoves.Add(move);


        foreach (Move move in allMoves)
        {
            board.MakeMove(move);
            int eval = evaluate(board, mySide, MAX_DEPTH_PLY);
            Console.WriteLine("The eval of " + move.ToString() + " is " + eval);
            if (eval > bestMoveEval)
            {
                bestMove = move;
                bestMoveEval = eval;
            }
            board.UndoMove(move);
        }
        Console.WriteLine("Best move: " + bestMove.ToString() + " with eval \x1b[34m" + bestMoveEval + "\x1b[0m");
        Console.WriteLine("------------------------------------------------------------------------");
        return bestMove;
    }

    int evaluate(Board board, int side, int maxDepth) {
        /**
         * Evaluates the board from the perspective of the side to move
         * Will be positive if the specified side is better, negative if worse
         * Side should be -1 (black) or 1 (white)
         */

        int eval = search(board, side, maxDepth);
        return eval;
    }

    int getMaterialDifference(Board board)
    {
        // Return +-1000000 if checkmate
        if (board.IsInCheckmate())
            return 1000000 * (board.IsWhiteToMove? -1 : 1);

        // Get the material difference between the two sides
        // Positive if white is ahead, negative if black is ahead
        PieceList[] allPieceLists = board.GetAllPieceLists();
        int materialDiff = 0;

        for (int i = 0; i < allPieceLists.Length; i++)
        {   
            materialDiff += (pieceValues[i % 6 + 1] * allPieceLists[i].Count) * (i < 6? 1 : -1);
        }
        return materialDiff;
    }

    int search(Board board, int side, int depth)
    /**
     * Depth should be in ply (move for one side)
     * Returns the best eval for the side to move
     *
    **/
    {
        if (depth == 0)
            return staticEval(board, side);
        // Console.WriteLine("Material diff (perspectivized) here is " + getMaterialDifference(board) * side + " for side " + side);

        int sideToMove = board.IsWhiteToMove? 1 : -1;
        int score = sideToMove == side? -1000000 : 1000000;

        // If the side to move is the same as the perspective,
        // we assume the eval of the position is the minimum achieveable
        // because the opponent will play their best move (max for them)
        foreach (Move move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            score = sideToMove == side? 
                Math.Max(score, search(board, side, depth - 1)) : 
                Math.Min(score, search(board, side, depth - 1));
            board.UndoMove(move);
        }

        return score;
    }

    int staticEval(Board board, int side)
    /**
     * Static evaluation without investigating any moves
    **/
    {
        int eval = getMaterialDifference(board) * side;
        return eval;
    }

    int measureDevelopment(Board board, bool forWhite)
    {
        //int isWhite
        //PieceList knights = board.GetAllPieceLists()[2 + ];

        return 0;
    }
}
