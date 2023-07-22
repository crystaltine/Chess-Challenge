using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 100, 290, 310, 500, 910, 100000 };
    int MAX_DEPTH_PLY;
    // int CLEAR_TRANSPOSITION_TABLE_AFTER_PLY = 4;

    int gamePhase = 0;

    // Store zobrist hash values : abs. evaluation
    Dictionary<ulong, int> transpositionTable = new Dictionary<ulong, int>();

    public Move Think(Board board, Timer timer)
    {

        // Clear the transposition table every 4 ply
        // if (board.PlyCount % 4 == 0) transpositionTable.Clear();

        int mySide = board.IsWhiteToMove? 1 : -1;
        gamePhase = getGamePhase(board);

        if (gamePhase == 5) MAX_DEPTH_PLY = 7;
        else if (gamePhase >= 4) MAX_DEPTH_PLY = 5;
        else MAX_DEPTH_PLY = 3;

        // Look for the highest eval. Eval function
        // evaluates from the perspective of our side, 
        // so we want the highest achievable
        int bestEval = -1000000 * mySide;
        Move bestMove = new();


        foreach (Move move in orderMoves(board))
        {   
            board.MakeMove(move);

            int eval = minimax(board, MAX_DEPTH_PLY, -1000000, 1000000, board.IsWhiteToMove);
            Console.WriteLine("\x1b[0m" + move.ToString() + " Eval: \x1b[34m" + eval + "\x1b[0m");
            if (eval * mySide >= bestEval * mySide)
            {
                bestEval = eval;
                bestMove = move;
            }
            board.UndoMove(move);
        }

        Console.WriteLine("Best move: " + bestMove.ToString() + " with eval \x1b[34m" + bestEval + "\x1b[0m");
        return bestMove;
    }

    
    int staticEval(Board board) {
        // Evaluate the board without looking ahead
        int eval = 0;

        //MATERIAL EVALUATION
        // Return +-1000000 if checkmate
        if (board.IsInCheckmate())
            return 1000000 * (board.IsWhiteToMove? -1 : 1);

        if (getGamePhase(board) >= 4) { // Endgame handling
            // In the endgame, both sides need to try and promote pawns
            // Its also important to keep the king active, so favor kings being in the center
            // Favor position for a side if they have a passed pawn

            // Pawn promotions & passer checking
            foreach (Piece P in board.GetAllPieceLists()[0].Concat(board.GetAllPieceLists()[6])) {

                

                // If a pawn is about to promote, 
                if (P.IsWhite && P.Square.Rank == 6)
                    // Add 500 now, if the promotion square is under control add 400 more <- ADD LATER
                    eval += 500;
                else if (!P.IsWhite && P.Square.Rank == 1)
                    eval -= 500;
            }
        }

        // Get the material difference between the two sides
        // Positive if white is ahead, negative if black is ahead
        PieceList[] allPieceLists = board.GetAllPieceLists();
        int[] openFiles = new int[8];

        for (int i = 0; i < allPieceLists.Length; i++)  
            eval += (pieceValues[i % 6 + 1] * allPieceLists[i].Count) * (i < 6? 1 : -1);

        //PUSH CENTER PAWNS
        foreach(Piece P in allPieceLists[0].Concat(allPieceLists[6])){
            openFiles[P.Square.File] = 1;

            if(getGamePhase(board) < 4 && (P.Square.File == 2 || P.Square.File == 3 || P.Square.File == 4) && 
                (P.Square.Rank == 3 || P.Square.Rank == 4))
                eval += (10 + P.Square.File) * (P.IsWhite? 1 : -1);
        }

        //MOVE KNIGHTS & BISHOPS AWAY FROM EDGES
        foreach(Piece N in allPieceLists[1].Concat(allPieceLists[7]).Concat(allPieceLists[2]).Concat(allPieceLists[8]))
            if(N.Square.File == 0 || N.Square.File == 7 || N.Square.Rank == 0 || N.Square.Rank == 7)
                eval -= 10 * (N.IsWhite? 1 : -1);

        //MOVE ROOKS TO OPEN FILES 
        foreach(Piece R in allPieceLists[3].Concat(allPieceLists[9])){
            if(openFiles[R.Square.File] == 0)
                eval += 9 * (R.IsWhite? 1 : -1);

            //places rooks on the 7th/2nd in the endgame
            if(getGamePhase(board) >= 4 && (R.Square.Rank == 3.5 + 2.5 * (R.IsWhite? 1 : -1)))
                eval += 10 * (R.IsWhite? 1 : -1);
        }

        return eval;
    }

    int getGamePhase(Board board) {
        /**
         * Returns:
         *
         * 0 if opening -           theory, development, memorized lines
         * 1 if early game -        development, piece activity, space
         * 2 if midgame -           attacking, defending, king safety
         * 3 if middle/late game -  maybe getting rooks out, attacking
         * 4 if endgame -           pawn play and piece coordination
         * 5 if late endgame -      promotion, king activity, checkmate
         *
         * Maximum starting material is 78 points in total
        **/
        int totalMaterial = -200000; // Kings shouldn't be counted
        PieceList[] allPieceLists = board.GetAllPieceLists();
        for (int i = 0; i < allPieceLists.Length; i++)
        {
            totalMaterial += pieceValues[i % 6 + 1] * allPieceLists[i].Count;
        }

        if (board.PlyCount <= 6) return 0;
        if (board.PlyCount <= 16) return 1;

        // Now based on material

        return 1 + (100 - totalMaterial) / 20;
    }

    List<Move> orderMoves(Board board){
        // Order moves by captures, promotions, checks, and then by least valuable victim
        // This will be used to order the moves in the minimax search
        List<Move> moves = Enumerable.ToList(board.GetLegalMoves());
        List<Move> orderedMoves = moves.Where(x => x.IsCapture || x.IsPromotion).ToList();
        moves = moves.Except(orderedMoves).ToList();
        orderedMoves.AddRange(moves);
        return orderedMoves;
    }

    int minimax(Board position, int depth, int alpha, int beta, bool maximizingPlayer) {
        
        // try { 
        //     int evalAndDepthSearched = transpositionTable[position.ZobristKey]; 
        //     if (evalAndDepthSearched >> 24 >= depth)
        //         return evalAndDepthSearched % 0xFFFFFF;
        // } catch {} // It doesn't exist in the transposition table, so we need to evaluate it (move on)

        if (depth == 0) 
            return staticEval(position);
        

        List<Move> orderedMoves = orderMoves(position);

        int bestEval = maximizingPlayer? -1000000 : 1000000;
        foreach (Move move in orderedMoves) {

            //discourage bad moves like Kf8 in the opening and middlegame
            int moveBonus = ((move.MovePieceType == PieceType.King && !move.IsCastles && gamePhase < 3)? -100 : 0);


            position.MakeMove(move);

            // If the game is drawn, return 0
            // if (position.IsDraw()) {
            //     position.UndoMove(move); 
            //     return 0;
            // }

            int eval = minimax(position, depth - 1, alpha, beta, !maximizingPlayer);
            position.UndoMove(move);
            
            if (maximizingPlayer) {
                bestEval = Math.Max(bestEval, eval) + moveBonus;
                alpha = Math.Max(alpha, eval);
                if (beta <= alpha)
                    break;
            } else {
                bestEval = Math.Min(bestEval, eval) - moveBonus;
                beta = Math.Min(beta, eval);
                if (beta <= alpha)
                    break;
            }
        }

        // transpositionTable[position.ZobristKey] = bestEval + 2^24 * depth; // First 8 bits represent the depth
        return bestEval;
    }
}
