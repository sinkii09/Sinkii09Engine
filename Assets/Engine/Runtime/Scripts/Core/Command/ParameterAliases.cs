namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Standard parameter aliases for consistent short-form script syntax across all commands.
    /// This ensures consistency and prevents conflicts between command parameters.
    /// </summary>
    public static class ParameterAliases
    {
        #region Core Variable/Value Parameters
        public const string Variable = "var";           // variable -> var
        public const string Value = "val";              // value -> val
        public const string Amount = "amount";          // amount stays the same (already short)
        public const string Type = "type";              // type stays the same (already short)
        #endregion

        #region Conditional/Comparison Parameters  
        public const string LeftVariable = "left";      // leftVar -> left
        public const string RightVariable = "right";    // rightVar/rightValue -> right
        public const string Operator = "op";            // operatorType -> op
        #endregion

        #region Flow Control Parameters
        public const string GotoLabel = "goto";         // gotoLabel -> goto
        public const string GotoLine = "line";          // gotoLine -> line
        public const string ElseLabel = "else";         // elseLabel -> else  
        public const string ElseLine = "elseLine";      // elseLine stays the same
        #endregion

        #region Loop/Counter Parameters
        public const string Count = "count";            // count stays the same
        public const string Counter = "counter";        // counterVar -> counter
        public const string Label = "label";            // label stays the same
        #endregion

        #region Boolean Control Parameters
        public const string Force = "force";            // forceType -> force
        public const string Show = "show";              // showMessage -> show
        public const string Create = "create";          // createIfMissing -> create
        public const string ZeroBased = "zero";         // zeroBasedCounter -> zero
        #endregion

        #region Range/Constraint Parameters
        public const string MinValue = "min";           // minValue -> min
        public const string MaxValue = "max";           // maxValue -> max
        public const string Reason = "reason";          // reason stays the same
        #endregion

        #region Source/Target Parameters
        public const string From = "from";              // rightVar (for copying) -> from
        public const string Target = "target";          // target parameter -> target
        public const string Source = "src";             // source parameter -> src
        #endregion

        #region Actor/Character Parameters
        public const string Actor = "actor";            // actorId -> actor
        public const string Character = "char";         // character -> char
        public const string Text = "text";              // dialogueText/text -> text
        public const string Position = "pos";           // position/targetPosition -> pos
        #endregion

        #region Common Modifiers
        public const string Duration = "dur";           // duration -> dur
        public const string Speed = "speed";            // speed stays the same
        public const string Color = "color";            // color stays the same
        #endregion
    }
}