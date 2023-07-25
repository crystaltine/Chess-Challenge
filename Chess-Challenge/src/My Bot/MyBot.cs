using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class TranspositionTableObject {
    public int Depth { get; set; }
    public int Evaluation { get; set; }
}
public class MyBot : IChessBot {
    # region CLASSVARS
    Dictionary<ulong, TranspositionTableObject> TranspositionTable = new (); // Lookup duplicate positions for speed
    int[] pieceValues = { 0, 95, 290, 310, 470, 910, 0 }; // Null P N B R Q K, can be changed based on gamephase
    int MAX_THINK_TIME = 4000; // milliseconds
    int DEPTH_EXTENSION_LIMIT = 4; // how many times we can extend the search for checks
    int TT_SIZE_LIMIT = 10_000_000; // Board keys are 8 bytes, SearchResult is list of ~5 moves of 2 bytes each ~= 18 bytes per entry
    int positionalEvalMultiplier = 1; // how much non-material based evals are worth
    int gamePhase = 0; // 0 opening, 1 early game, 2 midgame, 3 late midgame, 4 endgame, 5 late endgame
    ulong whitePawnAttacksBitboard; // all squares white's pawns can attack
    ulong blackPawnAttacksBitboard; // all squares black's pawns can attack
    Move bestMove; // bestmove found in each iteration of the search
    Move[] pv = new Move[20]; // chronologically ordered list of the current top engine line
    bool playingWhite; // Stores if in this game the bot is playing white
    int sideNegator; // -1/1 depending on black/white
    
    // stats
    int __nodes = 0; // Number of positions reached by search, excluding lookups
    int __transpositionLookups = 0; // number of lookups made each move calculation
    # endregion
    string getDisplayEval(int evaluation) {
        string displayEval = Math.Abs((double) evaluation / 100).ToString();
        // For displaying mate
        if (IsMate(evaluation)) {
            displayEval = "M" + (1000001 - Math.Abs(evaluation))/2;
        }
        // Add a + to the eval if 1. no mate, and 2. is positive
        if (evaluation > 0) displayEval = "+" + displayEval;
        else if (evaluation < 0) displayEval = "-" + displayEval;
        return displayEval;
    }
    bool IsMate(int evaluation) {
        return Math.Abs(evaluation) > 900000 && Math.Abs(evaluation) < 1000010;
    }
    int TryIncreaseMateDepth(int evaluation) {
        // If the search result is a mate, increase the depth by 1

        // Increase mate depth means -1 for white, +1 for black (since technically deeper mate is worse)
        if (IsMate(evaluation)) return evaluation + (evaluation > 0? -1 : 1);
        return evaluation; // not mate, just return back eval
    }
    public Move Think(Board board, Timer timer) {
        // Reset classparams for new search
        playingWhite = board.IsWhiteToMove;
        pv = new Move[20];
        sideNegator = playingWhite? 1 : -1;
        gamePhase = getGamePhase(board);
        whitePawnAttacksBitboard = blackPawnAttacksBitboard = 0;
        foreach (Piece P in board.GetAllPieceLists()[0])
            whitePawnAttacksBitboard |= BitboardHelper.GetPawnAttacks(P.Square, true);
        foreach (Piece P in board.GetAllPieceLists()[6])
            blackPawnAttacksBitboard |= BitboardHelper.GetPawnAttacks(P.Square, false);

        if (gamePhase > 3) pieceValues = new int[] { 0, 120, 285, 320, 560, 910, 0 }; // pawns and rooks much more important

        __nodes = 0;
        __transpositionLookups = 0;

        // Clear the transposition table if it gets too big
        if (TranspositionTable.Count > TT_SIZE_LIMIT) TranspositionTable.Clear();

        MainSearch(board, timer);
        return bestMove;
    }
    public void MainSearch(Board board, Timer timer) {
        bestMove = Move.NullMove;
        int searchDepth = 1;

        int currentDeepestEval = playingWhite? -1999999 : 1999999;

        while (true) {            

            // If it is mate there is no point in searching deeper
            if (timer.MillisecondsElapsedThisTurn > MAX_THINK_TIME || IsMate(currentDeepestEval)) { 
                // Cancel search but still keep the results from this iteration because of iterative deepening
                searchDepth--;
                break;
            }

            currentDeepestEval = Search(board, searchDepth, 0, -2999999, 2999999, playingWhite);
            searchDepth++;

            bestMove = pv[0];

            string pvWithoutNulls = string.Join(" ", pv.Where(x => !x.Equals(Move.NullMove)).Select(x => x.ToString()));
            Console.WriteLine("\u001b[48;5;130mdepth " + (searchDepth-1) + "\x1b[0m bestmove\x1b[32m " + bestMove + "\x1b[37m eval \x1b[36m" + getDisplayEval(currentDeepestEval) + "\x1b[37m nodes \x1b[35m" + __nodes + "\x1b[37m lookups\x1b[34m " + __transpositionLookups + "\x1b[37m tablesize\x1b[31m " + TranspositionTable.Count + "\x1b[37m time \x1b[2m" + timer.MillisecondsElapsedThisTurn + "ms \x1b[37m\x1b[0mpv \x1b[33m" + pvWithoutNulls + "\x1b[37m");
        }
    }
    int staticEval(Board board) {
        // Evaluate the board without looking ahead
        int eval = 0;
        int whiteMaterial = 0;
        int blackMaterial = 0;
        int gPhase = getGamePhase(board);
        // Trust that minimax will handle checkmates and draws

        // Get the material difference between the two sides
        // Positive if white is ahead, negative if black is ahead
        PieceList[] allPieceLists = board.GetAllPieceLists();

        for (int i = 0; i < 6; i++)
            whiteMaterial += pieceValues[i + 1] * allPieceLists[i].Count;
        for (int i = 6; i < allPieceLists.Length; i++)  
            blackMaterial += pieceValues[i - 5] * allPieceLists[i].Count;

        // Amplify material difference as the losing side has less and less material
        // this also encourages not trading pieces when losing
        // Equation for multiplier = mult = 5e^{-0.2(mat. of losing side)}+1
        double materialDifferenceMultipler = 5 * Math.Pow(Math.E, -0.2 * Math.Min(whiteMaterial, blackMaterial)) + 1;
        eval += (int) (materialDifferenceMultipler * (whiteMaterial - blackMaterial));

        if (gPhase >= 4) { // Endgame handling
            // In the endgame, both sides need to try and promote pawns
            // Its also important to keep the king active, so favor kings being in the center
            // Favor position for a side if they have a passed pawn

            //move king towards other king if material advantage exists
            // Max dist 14, min dist 2
            int distBetweenKings = Math.Abs(allPieceLists[5][0].Square.File - allPieceLists[11][0].Square.File) + Math.Abs(allPieceLists[5][0].Square.Rank - allPieceLists[11][0].Square.Rank);
            // Give the side that is winning better eval if kings are closer
            if (eval > 0) eval += (15 - distBetweenKings) * 25 * positionalEvalMultiplier;
            else eval -= (15 - distBetweenKings) * 25 * positionalEvalMultiplier;

            // Pawn promotions & passer checking
            foreach (Piece P in board.GetAllPieceLists()[0].Concat(board.GetAllPieceLists()[6])) {
                // If a pawn is about to promote, 
                if (P.IsWhite && P.Square.Rank >= 5)
                    eval += (150 * -(4-P.Square.Rank)) * positionalEvalMultiplier;
                else if (!P.IsWhite && P.Square.Rank <= 2)
                    eval -= (150 * (3-P.Square.Rank)) * positionalEvalMultiplier;
            }

            // King activity
            foreach (Piece K in board.GetAllPieceLists()[5].Concat(board.GetAllPieceLists()[11])) {
                eval -= (K.Square.File - 4) * (K.Square.File - 4) * (K.IsWhite? 1 : -1) * positionalEvalMultiplier;
                eval -= (K.Square.Rank - 4) * (K.Square.Rank - 4) * (K.IsWhite? 1 : -1) * positionalEvalMultiplier;
            }
            
            foreach (Piece P in board.GetAllPieceLists()[0].Concat(board.GetAllPieceLists()[6])) {
                if (P.IsWhite) {
                    if ((BitboardHelper.GetPawnAttacks(P.Square, true) & board.GetPieceBitboard(PieceType.Pawn, false)) == 0)
                        eval += 100 * positionalEvalMultiplier;
                } else {
                    if ((BitboardHelper.GetPawnAttacks(P.Square, false) & board.GetPieceBitboard(PieceType.Pawn, true)) == 0)
                        eval -= 100 * positionalEvalMultiplier;
                }
            }
        }

        int[] openFiles = new int[8];
        //PUSH CENTER PAWNS
        foreach(Piece P in allPieceLists[0].Concat(allPieceLists[6])) {
            openFiles[P.Square.File] = 1;
            
            // Attack center
            if(getGamePhase(board) < 3 && (BitboardHelper.GetPawnAttacks(P.Square, P.IsWhite) & 0b0000000000000000000000000001100000011000000000000000000000000000) != 0)
                eval += 50 / (gPhase + 1) * (P.IsWhite? 1 : -1) * positionalEvalMultiplier;
            
            // Center pawns
            if((P.Square.Rank == 3 || P.Square.Rank == 4) && (P.Square.File == 3 || P.Square.File == 4))
                eval += 80 / (gPhase + 1) * (P.IsWhite? 1 : -1) * positionalEvalMultiplier;
        }

        //CHECK KNIGHT CONTROL OF CENTER
        foreach(Piece N in allPieceLists[1].Concat(allPieceLists[7]))
            if(getGamePhase(board) < 3 && (BitboardHelper.GetKnightAttacks(N.Square) & 0b0000000000000000000000000001100000011000000000000000000000000000) != 0)
                eval += 80 / (gPhase + 1) * (N.IsWhite? 1 : -1) * positionalEvalMultiplier;

        //CHECK BISHOP CONTROL OF CENTER
        foreach(Piece B in allPieceLists[2].Concat(allPieceLists[8]))
            if(getGamePhase(board) < 3 && (BitboardHelper.GetSliderAttacks(PieceType.Bishop, B.Square, board.AllPiecesBitboard) & 0b0000000000000000000000000001100000011000000000000000000000000000) != 0)
                eval += 60 / (gPhase + 1) * (B.IsWhite? 1 : -1) * positionalEvalMultiplier;

        //MOVE ROOKS TO OPEN FILES 
        foreach(Piece R in allPieceLists[3].Concat(allPieceLists[9])) {
            if(openFiles[R.Square.File] == 0)
                eval += 16 * (R.IsWhite? 1 : -1) * positionalEvalMultiplier;

            //places rooks on the 7th/2nd in the endgame
            if(getGamePhase(board) >= 4 && (R.Square.Rank == 3.5 + 2.5 * (R.IsWhite? 1 : -1)))
                eval += 8 * (R.IsWhite? 1 : -1) * positionalEvalMultiplier;
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
         * 6 if 2 Kings -           insufficient material
         *
         * Maximum starting material is 78 points in total
        **/
        int pieceCount = 0; // counts # of pieces on the board
        PieceList[] allPieceLists = board.GetAllPieceLists();
        for (int i = 0; i < allPieceLists.Length; i+=1)
        {
            pieceCount += allPieceLists[i].Count;
        }

        if (board.PlyCount <= 6 && pieceCount > 29) return 0;
        if (board.PlyCount <= 16 && pieceCount > 25) return 1;

        // Now based on # of pieces on the board
        if (pieceCount == 2) return 6;
        return (40 - pieceCount)/7;
    }
    List<Move> OrderMoves(Board board, List<Move> priorityMoves, bool capturesOnly = false) {
        // Order moves by captures, promotions, checks, and then by least valuable victim
        // This will be used to order the moves in the minimax search
        Dictionary<Move, int> moves = board.GetLegalMoves(capturesOnly).ToDictionary(x => x, _ => 0);
        foreach (Move m in moves.Keys.Except(priorityMoves)) {

            int movePriority = 0;

            board.MakeMove(m);

            // If piece is not pawn, piece walks into enemy pawn attack, and move isnt capturing, discourage
            if (
                m.MovePieceType != PieceType.Pawn &&
                ((board.IsWhiteToMove? blackPawnAttacksBitboard : whitePawnAttacksBitboard) & 
                (board.IsWhiteToMove? board.WhitePiecesBitboard : board.BlackPiecesBitboard)) != 0 &&
                !m.IsCapture) movePriority -= 5;

            if (board.IsInCheck()) movePriority += 20;
            if (m.IsCapture) movePriority += 4 + 5 * (pieceValues[(int) m.CapturePieceType] - pieceValues[(int) m.MovePieceType]) / 100;
            if (m.IsPromotion) movePriority += 9;
            if (m.IsCastles) movePriority += 1;
            if (m.IsEnPassant) movePriority += 1;
            if(m.MovePieceType == PieceType.King && getGamePhase(board) < 4) movePriority -= 1;
            board.UndoMove(m);
            
            moves[m] = movePriority;
        }

        // Sort the moves by their priority
        priorityMoves.AddRange(moves.OrderByDescending(x => x.Value).Select(x => x.Key).ToList());
        return priorityMoves;
    }
    int Search(
        Board board, 
        int depthRemaining, 
        int depthFromRoot, 
        int whiteGuaranteed, // alpha
        int blackGuaranteed, // beta
        bool white, 
        int numExtensions = 0) {

        __nodes++;

        int dynamicSideNegator = board.IsWhiteToMove? 1 : -1;

        if (board.IsInCheckmate()) return 1000000 * -dynamicSideNegator;
        if (board.IsDraw()) return 0;
        if (depthRemaining == 0) return staticEval(board);//QuiescenceSearch(board, whiteGuaranteed, blackGuaranteed, white);

        List<Move> orderedMoves = bestMove.Equals(Move.NullMove) || playingWhite != white || depthFromRoot > 0? 
            OrderMoves(board, new List<Move>()) : 
            OrderMoves(board, new List<Move>() {bestMove});

        int bestEvalFound = white? -4999999 : 4999999;
        Move bestMoveFound = Move.NullMove;
        // Loop through child positions
        foreach (Move move in orderedMoves) {
            board.MakeMove(move);
            int resultEval; // initialize here because of transposition table

            // In early/midgame: kings moves bad, promotions good, move backwards bad, having less moves bad
            int moveBonus = ((move.MovePieceType == PieceType.King && !move.IsCastles && gamePhase < 3)? -125 : 0) * positionalEvalMultiplier;
            if (move.IsPromotion) moveBonus += 300 * positionalEvalMultiplier;
            if ((move.StartSquare.Rank - move.TargetSquare.Rank) * -dynamicSideNegator < 0 && getGamePhase(board) < 3) moveBonus -= 60 * positionalEvalMultiplier;
            moveBonus += 4 * gamePhase * gamePhase + 50 - board.GetLegalMoves().Length * 2;

            // Lookup position in table
            
            if (TranspositionTable.ContainsKey(board.ZobristKey) && TranspositionTable[board.ZobristKey].Depth >= depthRemaining) {
                __transpositionLookups++;
                resultEval = TranspositionTable[board.ZobristKey].Evaluation;
            } else {
                // Search extension for checks
                int extension = board.IsInCheck() && numExtensions < DEPTH_EXTENSION_LIMIT? 1 : 0;
                resultEval = Search(board, depthRemaining - 1 + extension, depthFromRoot + 1, whiteGuaranteed, blackGuaranteed, !white, numExtensions + extension);

                // Don't add moveBonus to mate evals, that will screw up mate depth
                // Also don't add multiple times; only add at the first depth
                if (!IsMate(resultEval) && depthFromRoot == 0) resultEval += 0 * moveBonus * dynamicSideNegator;

                TranspositionTable.TryAdd(
                    board.ZobristKey,
                    new TranspositionTableObject {
                        Depth = depthRemaining,
                        Evaluation = resultEval
                    }
                );
            
            }

            board.UndoMove(move);
            resultEval = TryIncreaseMateDepth(resultEval);

            // Comparing regular evals
            // See comments in SearchResult class for mate representation
            if (white? resultEval > bestEvalFound : resultEval < bestEvalFound) {
                bestEvalFound = resultEval;
                bestMoveFound = move;
            }

            // stop searching if the result is so good that the opponent will never allow it
            if ((white && resultEval > blackGuaranteed - 1 * dynamicSideNegator) ||
                (!white && resultEval < whiteGuaranteed - 1 * dynamicSideNegator)) {
                return resultEval;
            }

            // Update alpha/beta
            if (white) whiteGuaranteed = Math.Max(whiteGuaranteed, resultEval);
            else blackGuaranteed = Math.Min(blackGuaranteed, resultEval);
        }

        pv[depthFromRoot] = bestMoveFound;
        return bestEvalFound;
    }
    
    int QuiescenceSearch(Board board, int whiteGuaranteed, int blackGuaranteed, bool white) {
        int eval = staticEval(board);

        if (white && eval >= whiteGuaranteed) return eval;
        if (!white && eval <= blackGuaranteed) return eval;

        foreach (Move move in OrderMoves(board, new List<Move>(), true)) {
            board.MakeMove(move);
            int score = -QuiescenceSearch(board, -blackGuaranteed, -whiteGuaranteed, !white);
            board.UndoMove(move);

            if (white) {
                if (score >= whiteGuaranteed) return whiteGuaranteed;
                whiteGuaranteed = Math.Max(whiteGuaranteed, eval);
            } else {
                if (score <= blackGuaranteed) return blackGuaranteed;
                blackGuaranteed = Math.Min(blackGuaranteed, eval);
            }
        }
        return eval;
    }
    
}
