using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class EvaluationFunctions {
    static int[] piecesAttackingKingAreaEvals = {-33, -17, -14, -22, -41, -15}; // P N B R Q K
    public static ulong[] getWhiteAttacks(Board board) {
        ulong[] whiteControlledSquares = new ulong[6]; // P N B R Q K
        PieceList[] allWhitePieceLists = board.GetAllPieceLists().Take(6).ToArray();
        foreach (Piece P in allWhitePieceLists[0]) whiteControlledSquares[0] |= BitboardHelper.GetPawnAttacks(P.Square, true);
        foreach (Piece N in allWhitePieceLists[1]) whiteControlledSquares[1] |= BitboardHelper.GetKnightAttacks(N.Square);
        // TODO - does it matter much if we just pass in AllPiecesBitboard? since technically enemy pieces can be moved onto and friendly ones cant
        foreach (Piece B in allWhitePieceLists[2]) whiteControlledSquares[2] |= BitboardHelper.GetSliderAttacks(PieceType.Bishop, B.Square, board.AllPiecesBitboard);
        foreach (Piece R in allWhitePieceLists[3]) whiteControlledSquares[3] |= BitboardHelper.GetSliderAttacks(PieceType.Rook, R.Square, board.AllPiecesBitboard);
        foreach (Piece Q in allWhitePieceLists[4]) whiteControlledSquares[4] |= BitboardHelper.GetSliderAttacks(PieceType.Queen, Q.Square, board.AllPiecesBitboard);
        foreach (Piece K in allWhitePieceLists[5]) whiteControlledSquares[5] |= BitboardHelper.GetKingAttacks(K.Square);
        return whiteControlledSquares;
    }
    public static ulong[] getBlackAttacks(Board board) {
        ulong[] blackControlledSquares = new ulong[6]; // P N B R Q K
        PieceList[] allBlackPieceLists = board.GetAllPieceLists().Skip(6).ToArray();
        foreach (Piece P in allBlackPieceLists[0]) blackControlledSquares[0] |= BitboardHelper.GetPawnAttacks(P.Square, false);
        foreach (Piece N in allBlackPieceLists[1]) blackControlledSquares[1] |= BitboardHelper.GetKnightAttacks(N.Square);
        // TODO - does it matter much if we just pass in AllPiecesBitboard? since technically enemy pieces can be moved onto and friendly ones cant
        foreach (Piece B in allBlackPieceLists[2]) blackControlledSquares[2] |= BitboardHelper.GetSliderAttacks(PieceType.Bishop, B.Square, board.AllPiecesBitboard);
        foreach (Piece R in allBlackPieceLists[3]) blackControlledSquares[3] |= BitboardHelper.GetSliderAttacks(PieceType.Rook, R.Square, board.AllPiecesBitboard);
        foreach (Piece Q in allBlackPieceLists[4]) blackControlledSquares[4] |= BitboardHelper.GetSliderAttacks(PieceType.Queen, Q.Square, board.AllPiecesBitboard);
        foreach (Piece K in allBlackPieceLists[5]) blackControlledSquares[5] |= BitboardHelper.GetKingAttacks(K.Square);
        return blackControlledSquares;
    }
    public static int KnightBonus(Board board, PieceList whiteKnights, PieceList blackKnights, int gPhase, int positionalEvalMultiplier = 1) {
        // Bonus for controlling center 2x2
        int knightEval = 0;
        foreach(Piece N in whiteKnights.Concat(blackKnights))
            if (gPhase < 4 && ((BitboardHelper.GetKnightAttacks(N.Square) & 0b0000000000000000000000000001100000011000000000000000000000000000)) != 0)
                knightEval += (20 + 80 / (gPhase + 1)) * (N.IsWhite? 1 : -1) * positionalEvalMultiplier;
        return knightEval;
    }
    public static int BishopBonus(Board board, PieceList whiteBishops, PieceList blackBishops, int gPhase, int positionalEvalMultiplier = 1) {
        // Bonus for controlling center 4x4
        int bishopEval = 0;
        foreach(Piece B in whiteBishops.Concat(blackBishops))
            if(gPhase < 5 && ((BitboardHelper.GetSliderAttacks(PieceType.Bishop, B.Square, board.AllPiecesBitboard) & 0b0000000000000000001111000011110000111100001111000000000000000000)) != 0)
                bishopEval += (10 + 60 / (gPhase + 1)) * (B.IsWhite? 1 : -1) * positionalEvalMultiplier;
        return bishopEval;
    }
    public static int RookBonus(Board board, int[] openFiles, PieceList whiteRooks, PieceList blackRooks, int gPhase, int positionalEvalMultiplier = 1) {
        int rookEval = 0;
        foreach(Piece R in whiteRooks.Concat(blackRooks)) {
            int sideNegator = R.IsWhite? 1 : -1;

            // Bonus for controlling open files
            if(openFiles[R.Square.File] == 0)
                rookEval += 16 * sideNegator * positionalEvalMultiplier;

            // Incentivizes rooks on the 7th/2nd in the endgame
            if(gPhase >= 4 && (R.Square.Rank == 3.5 + 2.5 * sideNegator))
                rookEval += 8 * sideNegator * positionalEvalMultiplier;
        }
        return rookEval;
    }
    public static int KingSafety(Board board, Square kingLoc, int gPhase, bool whiteKing, int[] openFiles) {
        // Evaluates and gives bonuses/penalties to eval (pos/neg) based on the White king's safety
        int kingSafetyEval = 0; // relative for now, mult. by whiteking? 1:-1 at end

        // Before endgame, give bonus if king stays on top 5 common squares
        // For white: b1, c1, e1, g1, h1; for black: b8, c8, e8, g8, h8
        if (gPhase < 4) {
            int[] commonKingSquares = { 1, 2, 4, 6, 7 };
            if (commonKingSquares.Contains(kingLoc.File)) kingSafetyEval += 95;
        }

        ulong[] opponentAttacks = whiteKing? getBlackAttacks(board) : getWhiteAttacks(board);
        ulong kingSurroundings = BitboardHelper.GetKingAttacks(kingLoc);

        for (int i = 0; i < 6; i++) {
            int numEscapesControlledByPiece = BitboardHelper.GetNumberOfSetBits(opponentAttacks[i] & kingSurroundings);
            kingSafetyEval += numEscapesControlledByPiece * piecesAttackingKingAreaEvals[i] * (5-gPhase) / 2;
        }
        // Going away from 1st rank is bad until endgame
        if (gPhase < 4 && kingLoc.Rank != (whiteKing? 0:7)) kingSafetyEval -= 8 * (5-gPhase);

        // If there isn't at least 1 pawn/piece in the king's front diagonal 2 squares, that's bad
        if (gPhase < 4 && ((BitboardHelper.GetPawnAttacks(kingLoc, whiteKing) & board.GetPieceBitboard(PieceType.Pawn, whiteKing)) == 0)) 
            kingSafetyEval -= 23 * (5-gPhase);

        // If the king is on an open file, that's bad (if before endgame)
        if (gPhase < 4 && openFiles[kingLoc.File] == 0) kingSafetyEval -= 21 * (5-gPhase);

        return kingSafetyEval * (whiteKing? 1 : -1);
    }
}
public class TranspositionTableObject {
    public int Depth { get; set; }
    public SearchResult result { get; set; }
}
public class SearchResult {
    public int Evaluation { get; set; }
    public List<Move> PV { get; set; }
}
public class MyBot : IChessBot {
    # region CLASSVARS
    Dictionary<ulong, TranspositionTableObject> TranspositionTable = new (); // Lookup duplicate positions for speed
    int[] pieceValues = { 0, 95, 290, 310, 470, 910, 0 }; // Null P N B R Q K, can be changed based on gamephase
    int THINK_TIME = 5000; // milliseconds
    int DEPTH_EXTENSION_LIMIT = 4; // how many times we can extend the search for checks
    int TT_SIZE_LIMIT = 10_000_000; // Board keys are 8 bytes, SearchResult is list of ~5 moves of 2 bytes each ~= 18 bytes per entry
    int positionalEvalMultiplier = 1; // how much non-material based evals are worth
    int gamePhase = 0; // 0 opening, 1 early game, 2 midgame, 3 late midgame, 4 endgame, 5 late endgame
    ulong whitePawnAttacksBitboard; // all squares white's pawns can attack
    ulong blackPawnAttacksBitboard; // all squares black's pawns can attack
    int bestMoveEval; // eval for bestMove, updated synchronously
    Move bestMove; // bestmove found in each iteration of the search
    List<Move> bestMovePV; // A list containing the top line. When generated the deepest moves are first (reversed)
    bool playingWhite; // Stores if in this game the bot is playing white
    int sideNegator; // -1/1 depending on black/white
    bool searchCanceled;
    Timer timer;
    
    // stats
    int __nodes = 0; // Number of positions reached by search, excluding lookups
    int __transpositionLookups = 0; // number of lookups made each move calculation
    # endregion
    string getDisplayEval(int evaluation) {
        string displayEval = Math.Abs((double) evaluation / 100).ToString("0.00");
        // For displaying mate
        if (IsMate(evaluation)) {
            displayEval = "M" + (1000001 - Math.Abs(evaluation))/2;
        }
        // Add a + to the eval if 1. no mate, and 2. is positive
        if (evaluation > 0) displayEval = "+" + displayEval;
        else if (evaluation < 0) displayEval = "-" + displayEval;

        if (!IsMate(evaluation)) {
            // format to 2 decimal places

        }
        return displayEval;
    }
    bool IsMate(int evaluation) {
        return Math.Abs(evaluation) > 900000 && Math.Abs(evaluation) < 1000010;
    }
    int UpdateIfMate(int evaluation) {
        // If the search result is a mate, increase the depth by 1

        // Increase mate depth means -1 for white, +1 for black (since technically deeper mate is worse)
        if (IsMate(evaluation)) return evaluation + (evaluation > 0? -1 : 1);
        return evaluation; // not mate, just return back eval
    }
    public Move Think(Board board, Timer timer) {
        this.timer = timer;
        // THINK_TIME = (timer.OpponentMillisecondsRemaining < timer.MillisecondsRemaining? 500:0) + 200 + timer.MillisecondsRemaining / 40; // 1/40th of the time left, sometimes will go over a little (although it will go over less as time gets shorter)
        searchCanceled = false;
        playingWhite = board.IsWhiteToMove;
        sideNegator = playingWhite? 1 : -1;
        gamePhase = getGamePhase(board);
        whitePawnAttacksBitboard = blackPawnAttacksBitboard = 0;
        __nodes = 0;
        __transpositionLookups = 0;
        foreach (Piece P in board.GetAllPieceLists()[0])
            whitePawnAttacksBitboard |= BitboardHelper.GetPawnAttacks(P.Square, true);
        foreach (Piece P in board.GetAllPieceLists()[6])
            blackPawnAttacksBitboard |= BitboardHelper.GetPawnAttacks(P.Square, false);
        if (gamePhase > 3) pieceValues = new int[] { 0, 150, 285, 320, 560, 910, 0 }; // pawns and rooks much more important

        // Clear the transposition table if it gets too big
        if (TranspositionTable.Count > TT_SIZE_LIMIT) TranspositionTable.Clear();

        int deepestSearch = MainSearch(board, timer);

        // string pvWithoutNulls = string.Join(" ", pv.Where(x => !x.Equals(Move.NullMove)).Select(x => x.ToString()));
        // Console.WriteLine("\u001b[48;5;130mdepth " + (deepestSearch+1) + " ply\x1b[0m bestmove\x1b[32m " + bestMove + "\x1b[37m eval \x1b[36m" + getDisplayEval(bestMoveEval) + "\x1b[37m nodes \x1b[35m" + __nodes + "\x1b[37m lookups\x1b[34m " + __transpositionLookups + "\x1b[37m tablesize\x1b[31m " + TranspositionTable.Count + "\x1b[37m time \u001b[38;5;214m" + timer.MillisecondsElapsedThisTurn + "ms \x1b[37m\x1b[0mpv \x1b[33m" + pvWithoutNulls + "\x1b[37m\x1b[0m");
        Console.WriteLine("===================================================================================================================");
        return bestMove;
    }
    public int MainSearch(Board board, Timer timer) {
        int searchDepth = -1; // technically 1 lower than it should be since we loop in this method

        bestMove = Move.NullMove;
        bestMoveEval = playingWhite? -1999999 : 1999999;

        while (true) {
            searchDepth++; 
            Move bestMoveFoundThisDepth = Move.NullMove;
            int bestEvalFoundThisDepth = playingWhite? -2999999 : 2999999;
            List<Move> orderedMoves = OrderMoves(board, bestMove.Equals(Move.NullMove)? new List<Move>() : new List<Move>() {bestMove});
            
            foreach (Move move in orderedMoves) {
                board.MakeMove(move);
                SearchResult result = Search_V4(board, searchDepth, 0, -3999999, 3999999, !playingWhite);          
                board.UndoMove(move);

                if (result.Evaluation * sideNegator > bestEvalFoundThisDepth * sideNegator) {
                    bestEvalFoundThisDepth = result.Evaluation;
                    bestMoveFoundThisDepth = move;
                    result.PV.Add(move);
                    result.PV.Reverse();
                    bestMovePV = result.PV; 
                }

                if (timer.MillisecondsElapsedThisTurn > THINK_TIME) {
                    searchCanceled = true;
                    break;
                }
            }

            if (bestMoveFoundThisDepth.Equals(Move.NullMove) || bestMoveFoundThisDepth.Equals(bestMove)) break; // no moves found, so break out of loop and use the previous depth's best move

            string timeString = "\x1b[37mtime\u001b[38;5;214m " + timer.MillisecondsElapsedThisTurn + "ms\x1b[37m\x1b[0m";
            timeString += string.Concat(Enumerable.Repeat(" ", 38 - timeString.Length));
            string depthString = "\x1b[1m\u001b[38;2;251;96;27mdepth " + (searchDepth) + " ply\x1b[0m";
            depthString += string.Concat(Enumerable.Repeat(" ", 38 - depthString.Length));
            string bestMoveString = "\x1b[0mbestmove\x1b[32m " + bestMoveFoundThisDepth + "\x1b[37m";
            bestMoveString += string.Concat(Enumerable.Repeat(" ", 2));
            string bestEvalString = string.Format("\x1b[37meval\x1b[36m {0:0.00} \x1b[37m", getDisplayEval(bestEvalFoundThisDepth));
            bestEvalString += string.Concat(Enumerable.Repeat(" ", 29 - bestEvalString.Length));
            string nodesString = "\x1b[37mnodes\x1b[35m " + __nodes + "\x1b[37m";
            nodesString += string.Concat(Enumerable.Repeat(" ", 29 - nodesString.Length));
            string lookupsString = "\x1b[37mlookups\x1b[34m " + __transpositionLookups + "\x1b[37m";
            lookupsString += string.Concat(Enumerable.Repeat(" ", 32 - lookupsString.Length));
            string tablesizeString = "tablesize\x1b[31m " + TranspositionTable.Count + "\x1b[37m";
            tablesizeString += string.Concat(Enumerable.Repeat(" ", 33 - tablesizeString.Length));
            string pvWithoutNulls = "\x1b[37mpv\x1b[33m " + string.Join(" ", bestMovePV.Where(x => !x.Equals(Move.NullMove)).Select(x => x.ToString()));
            Console.WriteLine(string.Join(" ", new string[] {depthString, timeString, bestMoveString, bestEvalString, nodesString, lookupsString, tablesizeString, pvWithoutNulls}));

            bestMove = bestMoveFoundThisDepth;
            bestMoveEval = bestEvalFoundThisDepth;

            if (searchCanceled || IsMate(bestMoveEval))
                break;
        }
        return searchDepth;
    }
    int Evaluate(Board board) {
        // Evaluate the board without looking ahead
        int eval = 0;
        int whiteMaterial = 0;
        int blackMaterial = 0;
        int gPhase = getGamePhase(board);

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

            int whiteKingFile = allPieceLists[5][0].Square.File;
            int whiteKingRank = allPieceLists[5][0].Square.Rank;
            int blackKingFile = allPieceLists[11][0].Square.File;
            int blackKingRank = allPieceLists[11][0].Square.Rank;

            //move king towards other king if material advantage exists
            // Max dist 14, min dist 2
            int distBetweenKings = Math.Abs(whiteKingFile - blackKingFile) + Math.Abs(whiteKingRank - blackKingRank);

            // Calculate manhattan distance from king to corner -
            // The winning side wants to push opponent king to the corner
            int whiteKingCornerDist = Math.Min(whiteKingFile, 7 - whiteKingFile) + Math.Min(whiteKingRank, 7 - whiteKingRank);
            int blackKingCornerDist = Math.Min(blackKingFile, 7 - blackKingFile) + Math.Min(blackKingRank, 7 - blackKingRank);

            // Give the side that is winning better eval if kings are closer and other king is closer to corner
            if (eval > 0) {
                eval += (15 - distBetweenKings) * 25 * positionalEvalMultiplier + (15 - blackKingCornerDist) * 25;
            }
            else {
                eval -= (15 - distBetweenKings) * 25 * positionalEvalMultiplier + (15 - whiteKingCornerDist) * 25;
            }
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

        int[] openFiles = new int[8]; // 0 = open, 1 = closed
        
        // TODO - outposts, development, doubled and isolated pawns

        // Push center pawns and update open files
        foreach(Piece P in allPieceLists[0].Concat(allPieceLists[6])) {
            openFiles[P.Square.File] = 1;
            
            // Attack center
            if(getGamePhase(board) < 3 && (BitboardHelper.GetPawnAttacks(P.Square, P.IsWhite) & 0b0000000000000000000000000001100000011000000000000000000000000000) != 0)
                eval += 50 / (gPhase + 1) * (P.IsWhite? 1 : -1) * positionalEvalMultiplier;
            
            // Center pawns
            if((P.Square.Rank == 3 || P.Square.Rank == 4) && (P.Square.File == 3 || P.Square.File == 4))
                eval += 80 / (gPhase + 1) * (P.IsWhite? 1 : -1) * positionalEvalMultiplier;
        }

        eval += EvaluationFunctions.KnightBonus(board, allPieceLists[2], allPieceLists[8], gamePhase);
        eval += EvaluationFunctions.BishopBonus(board, allPieceLists[3], allPieceLists[9], gamePhase);
        eval += EvaluationFunctions.RookBonus(board, openFiles, allPieceLists[4], allPieceLists[10], gamePhase);

        // King safety
        eval += EvaluationFunctions.KingSafety(board, board.GetKingSquare(true), gamePhase, true, openFiles);
        eval += EvaluationFunctions.KingSafety(board, board.GetKingSquare(false), gamePhase, false, openFiles);
        
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
    int QuiescenceSearch(Board board, int alpha, int beta) {
        int staticEval = Evaluate(board) * (board.IsWhiteToMove? 1 : -1);

        if (staticEval >= beta) return beta;
        alpha = Math.Max(alpha, staticEval);

        foreach(Move m in OrderMoves(board, new List<Move>(), true))  {
            board.MakeMove(m);
            int score = -QuiescenceSearch(board, -beta, -alpha);
            board.UndoMove(m);

            if (score >= beta) return beta;
            alpha = Math.Max(alpha, score);
        }

        return alpha;
    }
    SearchResult Search_V4(Board board, int depthRemaining, int depthFromRoot, int alpha, int beta, bool maximizing, int numExtensions = 0) {
        __nodes++;
        int dynamicSideNegator = board.IsWhiteToMove? 1 : -1;

        if (board.IsInCheckmate()) return new SearchResult {
            Evaluation = -1000000 * dynamicSideNegator,
            PV = new()
        };
        if (board.IsDraw()) return new SearchResult {
            Evaluation = 0,
            PV = new()
        };
        if (depthRemaining <= 0) {
            int param_1 = board.IsWhiteToMove? alpha : -beta;
            int param_2 = board.IsWhiteToMove? beta : -alpha;
            int eval_raw = QuiescenceSearch(board, param_1, param_2);// * dynamicSideNegator;
            int eval = eval_raw * dynamicSideNegator;
            return new SearchResult {
            Evaluation = eval,
            PV = new()
        };}

        SearchResult bestResult = new SearchResult {
            Evaluation = maximizing? -4999999 : 4999999,
            PV = new()
        };
        Move bestMove = Move.NullMove;

        foreach (Move move in OrderMoves(board, new List<Move> ())) {
            board.MakeMove(move);

            SearchResult result;
            if (TranspositionTable.ContainsKey(board.ZobristKey) && TranspositionTable[board.ZobristKey].Depth >= depthRemaining) {
                __transpositionLookups++;
                result = TranspositionTable[board.ZobristKey].result;
            } 
            else {
                int extension = (board.IsInCheck() || move.IsPromotion) && numExtensions < DEPTH_EXTENSION_LIMIT? 1 : 0;
                result = Search_V4(board, depthRemaining - 1 + extension, depthFromRoot + 1, alpha, beta, !maximizing, numExtensions + extension); 
                result.Evaluation = UpdateIfMate(result.Evaluation);

                TranspositionTable.TryAdd(
                    board.ZobristKey,
                    new TranspositionTableObject {
                        Depth = depthRemaining,
                        result = result
                    }
                );
            }

            board.UndoMove(move);

            int score = result.Evaluation;

            if (maximizing) {
                // If score exceeds beta no need to keep searching
                if (score >= beta) return new SearchResult{Evaluation=score, PV=result.PV.Append(move).ToList()};
                if (score > bestResult.Evaluation) {
                    bestResult = result;
                    bestMove = move;
                }
                alpha = Math.Max(alpha, score);
            } else {
                // If score under alpha no need to keep searching
                if (score <= alpha) return new SearchResult{Evaluation=score, PV=result.PV.Append(move).ToList()};
                if (score < bestResult.Evaluation) {
                    bestResult = result;
                    bestMove = move;
                }
                beta = Math.Min(beta, score);
            }
        }
        return new SearchResult {
            Evaluation = bestResult.Evaluation,
            PV = bestResult.PV.Append(bestMove).ToList()
        };
    }
}
