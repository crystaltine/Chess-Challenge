using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 90, 290, 310, 500, 910, 100000 };
    int MAX_DEPTH = 4;
    int positionalEvalMultiplier = 1;
    int gamePhase = 0;
    int nodes = 0;
    
    public Move Think(Board board, Timer timer) {
        // Reset classparams for new search
        gamePhase = getGamePhase(board);
        nodes = 0;

        int mySide = board.IsWhiteToMove? 1 : -1;

        //SearchResult bestSearchResult = minimax_from_scratch(board, MAX_DEPTH, 0, null, null, board.IsWhiteToMove);
        SearchResult bestSearchResult = new SearchResult {
            Evaluation = board.IsWhiteToMove? -4999999 : 4999999,
            PrincipalVariation = new List<Move>()
        };
        
        foreach (Move move in orderMoves(board)) {
            board.MakeMove(move);
            SearchResult result = minimax_from_scratch(board, MAX_DEPTH, 0, null, null, board.IsWhiteToMove);
            Console.WriteLine("\x1b[0m" + move.ToString() + " eval: \x1b[32m" + result.Evaluation + "\x1b[0m");
            board.UndoMove(move);

            // If we are white looking at our possible moves, we will keep the best move we can find.
            if (board.IsWhiteToMove) {
                if (result.Evaluation > bestSearchResult.Evaluation) {
                    bestSearchResult = result;
                    bestSearchResult.PrincipalVariation.Add(move); // reverse this later
                }
            } else {
                if (result.Evaluation < bestSearchResult.Evaluation) {
                    bestSearchResult = result;
                    bestSearchResult.PrincipalVariation.Add(move); // reverse this later
                    // Console.WriteLine("Added move to pv: " + move.ToString() + " so it is now " + string.Join(" ", bestSearchResult.PrincipalVariation.Select(x => x.ToString())));
                }
            }
        }
        
        
        // Reverse the PV since it was built from the leaf node up
        bestSearchResult.PrincipalVariation.Reverse();

        string displayEval = Math.Abs((double) bestSearchResult.Evaluation / 100).ToString();
        // For displaying mate
        if (bestSearchResult.IsMate()) {
            displayEval = "#" + (1000001 - Math.Abs(bestSearchResult.Evaluation))/2;
        }
        // Add a + to the eval if 1. no mate, and 2. is positive
        if (bestSearchResult.Evaluation > 0) displayEval = "+" + displayEval;
        else if (bestSearchResult.Evaluation < 0) displayEval = "-" + displayEval;

        Console.WriteLine("\x1b[32m bestmove " + bestSearchResult.PrincipalVariation[0] + "\x1b[0m eval \x1b[36m" + displayEval + "\x1b[0m nodes \x1b[35m" + nodes + "\x1b[0m" + " pv \x1b[33m" + string.Join(" ", bestSearchResult.PrincipalVariation.Select(x => x.ToString())) + "\x1b[0m");
        return bestSearchResult.PrincipalVariation[0];
    }
    int staticEval(Board board) {
        // Evaluate the board without looking ahead
        int eval = 0;

        // Trust that minimax will handle checkmates

        if (board.IsDraw()) return 0;


        // Get the material difference between the two sides
        // Positive if white is ahead, negative if black is ahead
        PieceList[] allPieceLists = board.GetAllPieceLists();
        int[] openFiles = new int[8];

        for (int i = 0; i < allPieceLists.Length; i+=1)  
            eval += (pieceValues[i % 6 + 1] * allPieceLists[i].Count) * (i < 6? 1 : -1);

        if(getGamePhase(board) == 5){
            //move king towards other king if material advantage exists
            if(eval > 0 && board.IsWhiteToMove){
                eval -= 5 * (board.IsWhiteToMove? 1 : -1) * (board.GetAllPieceLists()[5][0].Square.File - board.GetAllPieceLists()[11][0].Square.File) * (board.GetAllPieceLists()[5][0].Square.File - board.GetAllPieceLists()[11][0].Square.File) * 10 * positionalEvalMultiplier;
                eval -= 5 * (board.IsWhiteToMove? 1 : -1) * (board.GetAllPieceLists()[5][0].Square.Rank - board.GetAllPieceLists()[11][0].Square.Rank) * (board.GetAllPieceLists()[5][0].Square.Rank - board.GetAllPieceLists()[11][0].Square.Rank) * 10 * positionalEvalMultiplier;
            }
            else {
                eval += 5 * (board.IsWhiteToMove? 1 : -1) * (board.GetAllPieceLists()[5][0].Square.File - board.GetAllPieceLists()[11][0].Square.File) * (board.GetAllPieceLists()[5][0].Square.File - board.GetAllPieceLists()[11][0].Square.File) * 10 * positionalEvalMultiplier;
                eval += 5 * (board.IsWhiteToMove? 1 : -1) * (board.GetAllPieceLists()[5][0].Square.Rank - board.GetAllPieceLists()[11][0].Square.Rank) * (board.GetAllPieceLists()[5][0].Square.Rank - board.GetAllPieceLists()[11][0].Square.Rank) * 10 * positionalEvalMultiplier;
            }

        }

        if (getGamePhase(board) >= 4) { // Endgame handling
            // In the endgame, both sides need to try and promote pawns
            // Its also important to keep the king active, so favor kings being in the center
            // Favor position for a side if they have a passed pawn

            // Pawn promotions & passer checking
            foreach (Piece P in board.GetAllPieceLists()[0].Concat(board.GetAllPieceLists()[6])) {
                // If a pawn is about to promote, 
                if (P.IsWhite && P.Square.Rank >= 5)
                    // Add 500 now, if the promotion square is under control add 400 more <- ADD LATER
                    eval += (150 * -(4-P.Square.Rank)) * positionalEvalMultiplier;
                else if (!P.IsWhite && P.Square.Rank <= 2)
                    eval -= (150 * (3-P.Square.Rank)) * positionalEvalMultiplier;
            }

            // King activity
            foreach (Piece K in board.GetAllPieceLists()[5].Concat(board.GetAllPieceLists()[11])) {
                eval -= (K.Square.File - 4) * (K.Square.File - 4) * (K.IsWhite? 1 : -1) * positionalEvalMultiplier;
                eval -= (K.Square.Rank - 4) * (K.Square.Rank - 4) * (K.IsWhite? 1 : -1) * positionalEvalMultiplier;
            }
            /*// Passed pawn checking
            foreach (Piece P in board.GetAllPieceLists()[0].Concat(board.GetAllPieceLists()[6])) {
                if (P.IsWhite) {
                    if ((BitboardHelper.GetPawnAttacks(P.Square, true) & board.GetAllPieceLists()[6].GetBitboard()) == 0)
                        eval += 100 * positionalEvalMultiplier;
                } else {
                    if ((BitboardHelper.GetPawnAttacks(P.Square, false) & board.GetAllPieceLists()[0].GetBitboard()) == 0)
                        eval -= 100 * positionalEvalMultiplier;
                }
            }*/
        }

        //PUSH CENTER PAWNS
        foreach(Piece P in allPieceLists[0].Concat(allPieceLists[6])){
            openFiles[P.Square.File] = 1;

            if(getGamePhase(board) < 3 && (BitboardHelper.GetPawnAttacks(P.Square, P.IsWhite) & 0b0000000000000000000000000001100000011000000000000000000000000000) != 0)
                eval += 4 * (P.IsWhite? 1 : -1) * positionalEvalMultiplier;
            
            if((P.Square.Rank == 3 || P.Square.Rank == 4) && (P.Square.File == 3 || P.Square.File == 4))
                eval += 8 * (P.IsWhite? 1 : -1) * positionalEvalMultiplier;
        }

        //CHECK KNIGHT CONTROL OF CENTER
        foreach(Piece N in allPieceLists[1].Concat(allPieceLists[7]))
            if(getGamePhase(board) < 3 && (BitboardHelper.GetKnightAttacks(N.Square) & 0b0000000000000000000000000001100000011000000000000000000000000000) != 0)
                eval += 6 * (N.IsWhite? 1 : -1) * positionalEvalMultiplier;

        //CHECK BISHOP CONTROL OF CENTER
        foreach(Piece B in allPieceLists[2].Concat(allPieceLists[8]))
            if(getGamePhase(board) < 3 && (BitboardHelper.GetSliderAttacks(PieceType.Bishop, B.Square, board.AllPiecesBitboard) & 0b0000000000000000000000000001100000011000000000000000000000000000) != 0)
                eval += 5 * (B.IsWhite? 1 : -1) * positionalEvalMultiplier;

        //MOVE ROOKS TO OPEN FILES 
        foreach(Piece R in allPieceLists[3].Concat(allPieceLists[9])){
            if(openFiles[R.Square.File] == 0)
                eval += 4 * (R.IsWhite? 1 : -1) * positionalEvalMultiplier;

            //places rooks on the 7th/2nd in the endgame
            if(getGamePhase(board) >= 4 && (R.Square.Rank == 3.5 + 2.5 * (R.IsWhite? 1 : -1)))
                eval += 6 * (R.IsWhite? 1 : -1) * positionalEvalMultiplier;
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

        if (board.PlyCount <= 6) return 0;
        if (board.PlyCount <= 16) return 1;

        // Now based on # of pieces on the board
        if (pieceCount == 2) return 6;
        return (40 - pieceCount)/7;
    }
    List<Move> orderMoves(Board board){
        // Order moves by captures, promotions, checks, and then by least valuable victim
        // This will be used to order the moves in the minimax search
        Dictionary<Move, int> moves = board.GetLegalMoves().ToDictionary(x => x, _ => 0);
        foreach (Move m in moves.Keys) {

            int movePriority = 0;

            board.MakeMove(m);
            if (board.IsInCheck()) movePriority += 20;
            if (m.IsCapture) movePriority += 4 * (pieceValues[(int) m.CapturePieceType] - pieceValues[(int) m.MovePieceType]) / 100;
            if (m.IsPromotion) movePriority += 9;
            if (m.IsCastles) movePriority += 1;
            if (m.IsEnPassant) movePriority += 1;
            board.UndoMove(m);
        }

        // Sort the moves by their priority
        return moves.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
    }
    public class SearchResult {

        // CHECKMATE REPRESENTATION

        // If in the CURRENT POSITION, black is checkmated, return 1,000,000
        // If white can make one move and checkmate black, return 999,999
        // If black makes any move and then white can checkmate, return 999,998,
        // And so on. The evaluation will be 1M-#ply to get to mate if white
        // can force a checkmate. If black can force a checkmate, the evaluation
        // the opposite. -1,000,000 is white is checkmated currently, -999,999
        // if black can force a checkmate with the next move, and so on.
        // This allows Search to compare different checkmates in the same way that
        // regular evals are compared, because better checkmates are represented
        // by more "attractive" evaluations (e.g. 999,999 (#1ply) is better than
        // 999,996 (#4ply) in the same way 900 > 500 for white)
        public int Evaluation { get; set; }

        // NOTE: with minimax_from_scratch, this is reversed for optimization
        public List<Move> PrincipalVariation { get; set; }

        public bool IsMate() {
            return Math.Abs(Evaluation) >= 900000; // Leave some space for mate representation
        }

        public void TryIncreaseMateDepth() {
            if (IsMate()) Evaluation -= 1 * Math.Sign(Evaluation);
        }

        public SearchResult copy() {
            return new SearchResult {
                Evaluation = Evaluation,
                PrincipalVariation = new List<Move>(PrincipalVariation)
            };
        }
    }
    SearchResult minimax_from_scratch(Board board, int depthRemaining, int depthFromRoot, SearchResult? whiteGuaranteed, SearchResult? blackGuaranteed, bool white) {
        // Visited another node, update stat
        nodes+=1;

        if (board.IsInCheckmate())
            return new SearchResult {
                // Return maximum unfavorable eval for the
                // side whose turn it is to move. See
                // comments in SearchResult class.
                Evaluation = 1000000 * (white? -1 : 1),
                PrincipalVariation = new List<Move>()
            };

        // Check for end of search
        // If we are at the end, return the static evaluation and an empty PV
        if (depthRemaining == 0)
            return new SearchResult {
                Evaluation = staticEval(board),
                PrincipalVariation = new List<Move>()
            };

        SearchResult bestSearchResult = new SearchResult {
            Evaluation = white? -4999999 : 4999999,
            PrincipalVariation = new List<Move>()
        };
        // Loop through child positions
        if (white) {

            // Note: whiteGuaranteed was passed down to us from the level above. This means
            // white is guaranteed to be able to reach a position with at least that eval
            // if we hit this position. If we find a position with a higher eval, we can
            // use whiteGuaranteed to tell searches below this level that we also have an
            // eval that is at least as good as whiteGuaranteed.

            foreach (Move move in orderMoves(board)) {
                board.MakeMove(move);

                // Assume that this gets the correct evaluations for all child positions
                // SearchResult? whiteGuaranteedCopy = whiteGuaranteed?.copy();
                // SearchResult? blackGuaranteedCopy = blackGuaranteed?.copy();
                SearchResult result = minimax_from_scratch(board, depthRemaining - 1, depthFromRoot + 1, whiteGuaranteed, blackGuaranteed, false);
                
                board.UndoMove(move);
                result.TryIncreaseMateDepth();
                //if (result.IsMate()) result.Evaluation-=1;

                // if (move.StartSquare.Name.Equals("c1") && move.TargetSquare.Name.Equals("a1") && board.GetFenString().StartsWith("6r1/1r4b1/k3pp1p/1p2p3/8/3Q1P2/5KPP/2R5")) {
                //     Console.WriteLine("Piece: " + move.MovePieceType + " " + move.StartSquare.Name + " -> " + move.TargetSquare.Name + ", wG, bG, result");
                //     Console.WriteLine(whiteGuaranteed?.Evaluation + " " + blackGuaranteed?.Evaluation + " " + result.Evaluation);
                // }

                // If we are white looking at our possible moves, we will keep the best move we can find.
                // In this case, it is the child position with the highest eval. We are guaranteed to be
                // able to reach it if we hit the position at THIS DEPTH.
                // look for the maximum eval we can get from all our possible moves

                if (blackGuaranteed != null && result.Evaluation >= blackGuaranteed.Evaluation - 1) {
                    // If we are white and we find a move that is better than blackGuaranteed, we can
                    // stop searching because we know black will never allow us to get to this position
                    // since they have a better move to take.
                    // Return the eval we found here (it is very big so black won't take it anyways on the upper levels)
                    result.PrincipalVariation.Add(move); // reverse this later
                    return result;
                }

                // Set whiteGuaranteed to the best eval we can get from this position
                if (whiteGuaranteed == null || result.Evaluation > whiteGuaranteed.Evaluation) 
                    whiteGuaranteed = result;

                // Comparing regular evals
                // See comments in SearchResult class for mate representation
                if (result.Evaluation > bestSearchResult.Evaluation) {
                    bestSearchResult = result;
                    bestSearchResult.PrincipalVariation.Add(move); // reverse this later
                }
            }
            // Here at the end, we have the best SearchResult we can get; it represents
            // the highest eval we can get from this position and the path to get it FROM
            // THE CURRENT NODE. It is also updated with the move we would take to get to
            // the leaf node with the highest eval.

            // If bestSearchResult is a mate, we need to add one to the mate depth since we are
            // one level above the position where mate was found. (For white, longer mates are
            // represented by lower (~worse) evals, so we subtract one)
            
            // If it is not mate, we don't need to do anything since we are already have the
            // best eval reachable from here, and we've updated PV with the move we would take.
            
            // We can now return the bestSearchResult to the parent node, which will use it
            // to make the decision of which move to take.
            // if (bestSearchResult.IsMate()) bestSearchResult.Evaluation-=1;
            return bestSearchResult;

        } 
        else { 
            
            // See above note for whiteGuaranteed

            // Black does the opposite (lowers eval, higher mate eval is faster)
            foreach (Move move in orderMoves(board)) {
                board.MakeMove(move);

                //SearchResult? whiteGuaranteedCopy = whiteGuaranteed?.copy();
                //SearchResult? blackGuaranteedCopy = blackGuaranteed?.copy();
                // Assume that this gets the correct evaluations for all child positions
                SearchResult result = minimax_from_scratch(board, depthRemaining - 1, depthFromRoot + 1, whiteGuaranteed, blackGuaranteed, true);
                
                board.UndoMove(move);
                result.TryIncreaseMateDepth();
                // if (result.IsMate()) result.Evaluation += 1;

                // If we are black looking at our possible moves, we will keep the best move we can find.
                // In this case, it is the child position with the lowest eval. We are guaranteed to be
                // able to reach it if we hit the position at THIS DEPTH.
                // look for the minimum eval we can get from all our possible moves

                // If one of our moves is really good for us (black), we will definitely take it.
                // However, if the upper levels of the search have already found a move white will
                // probably take to avoid being in this bad situation, we don't need to worry about
                // any other evals, because we know white won't allow us to get to this position.
                // So, if we find a move that is better than whiteGuaranteed, we can stop searching
                if (whiteGuaranteed != null && result.Evaluation <= whiteGuaranteed.Evaluation) {
                    // If we are black and we find a move that is worse than whiteGuaranteed, we can
                    // stop searching because we know white will never allow us to get to this position
                    // since they have a better move to take.
                    
                    result.PrincipalVariation.Add(move); // reverse this later
                    //if (result.IsMate()) result.Evaluation += 1;
                    return result;
                }

                // Note: if xGuaranteed null, that means there is currently no better option for
                // player x so there is no pruning to be done yet, but they will accept any move
                // This is why we will set blackGuaranteed if it is is null.

                // Set blackGuaranteed to the best eval we can get from this position;
                // it is the new best move we have at this depth
                if (blackGuaranteed == null || result.Evaluation < blackGuaranteed.Evaluation) 
                    blackGuaranteed = result;

                // Comparing regular evals
                // See comments in SearchResult class for mate representation
                if (result.Evaluation < bestSearchResult.Evaluation) {
                    bestSearchResult = result;
                    bestSearchResult.PrincipalVariation.Add(move); // reverse this later
                }
            }
            // Here at the end, we have the best SearchResult we can get; it represents
            // the highest eval we can get from this position and the path to get it FROM
            // THE CURRENT NODE. It is also updated with the move we would take to get to
            // the leaf node with the highest eval.

            // If bestSearchResult is a mate, we need to add one to the mate depth since we are
            // one level above the position where mate was found. (For black, longer mates are
            // represented by higher (~worse) evals, so we add one)
            // if (bestSearchResult.IsMate()) bestSearchResult.Evaluation+=1;
            
            // If it is not mate, we don't need to do anything since we are already have the
            // best eval reachable from here, and we've updated PV with the move we would take.

            // We can now return the bestSearchResult to the parent node, which will use it
            // to make the decision of which move to take.
            //if (bestSearchResult.IsMate()) bestSearchResult.Evaluation+=1;
            return bestSearchResult;
        }
    }
}