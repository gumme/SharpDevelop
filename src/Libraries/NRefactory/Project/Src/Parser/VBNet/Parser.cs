
#line  1 "VBNET.ATG" 
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using ICSharpCode.NRefactory.Parser.AST;
using ICSharpCode.NRefactory.Parser.VB;
using ASTAttribute = ICSharpCode.NRefactory.Parser.AST.Attribute;
/*
  Parser.frame file for NRefactory.
 */
using System;
using System.Reflection;

namespace ICSharpCode.NRefactory.Parser.VB {



internal class Parser : AbstractParser
{
	const int maxT = 206;

	const  bool   T            = true;
	const  bool   x            = false;
	

#line  12 "VBNET.ATG" 
private string assemblyName = null;
private Stack withStatements;
private StringBuilder qualidentBuilder = new StringBuilder();

public string ContainingAssembly
{
	set { assemblyName = value; }
}
Token t
{
	get {
		return lexer.Token;
	}
}
Token la
{
	get {
		return lexer.LookAhead;
	}
}

/* Return the n-th token after the current lookahead token */
void StartPeek()
{
	lexer.StartPeek();
}

Token Peek()
{
	return lexer.Peek();
}

Token Peek (int n)
{
	lexer.StartPeek();
	Token x = la;
	while (n > 0) {
		x = lexer.Peek();
		n--;
	}
	return x;
}

public void Error(string s)
{
	if (errDist >= minErrDist) {
		errors.Error(la.line, la.col, s);
	}
	errDist = 0;
}

public override Expression ParseExpression()
{
	Expression expr;
	Expr(out expr);
	return expr;
}

bool LeaveBlock()
{
  int peek = Peek(1).kind;
  return Tokens.BlockSucc[la.kind] && (la.kind != Tokens.End || peek == Tokens.EOL || peek == Tokens.Colon);
}

/* True, if "." is followed by an ident */
bool DotAndIdentOrKw () {
	int peek = Peek(1).kind;
	return la.kind == Tokens.Dot && (peek == Tokens.Identifier || peek >= Tokens.AddHandler);
}

bool IsEndStmtAhead()
{
	int peek = Peek(1).kind;
	return la.kind == Tokens.End && (peek == Tokens.EOL || peek == Tokens.Colon);
}

bool IsNotClosingParenthesis() {
	return la.kind != Tokens.CloseParenthesis;
}

/*
	True, if ident is followed by "="
*/
bool IdentAndAsgn () {
	if(la.kind == Tokens.Identifier) {
		if(Peek(1).kind == Tokens.Assign) return true;
		if(Peek(1).kind == Tokens.Colon && Peek(2).kind == Tokens.Assign) return true;
	}
	return false;
}

/*
	True, if ident is followed by "=" or by ":" and "="
*/
bool IsNamedAssign() {
//	if(Peek(1).kind == Tokens.Assign) return true; // removed: not in the lang spec
	if(Peek(1).kind == Tokens.Colon && Peek(2).kind == Tokens.Assign) return true;
	return false;
}

bool IsObjectCreation() {
	return la.kind == Tokens.As && Peek(1).kind == Tokens.New;
}

/*
	True, if "<" is followed by the ident "assembly" or "module"
*/
bool IsGlobalAttrTarget () {
	Token pt = Peek(1);
	return la.kind == Tokens.LessThan && ( string.Equals(pt.val, "assembly", StringComparison.InvariantCultureIgnoreCase) || string.Equals(pt.val, "module", StringComparison.InvariantCultureIgnoreCase));
}

/*
	True if the next token is a "(" and is followed by "," or ")"
*/
bool IsDims()
{
	int peek = Peek(1).kind;
	return la.kind == Tokens.OpenParenthesis
						&& (peek == Tokens.Comma || peek == Tokens.CloseParenthesis);
}

bool IsSize()
{
	return la.kind == Tokens.OpenParenthesis;
}

/*
	True, if the comma is not a trailing one,
	like the last one in: a, b, c,
*/
bool NotFinalComma() {
	int peek = Peek(1).kind;
	return la.kind == Tokens.Comma &&
		   peek != Tokens.CloseCurlyBrace;
}

/*
	True, if the next token is "Else" and this one
	if followed by "If"
*/
bool IsElseIf()
{
	int peek = Peek(1).kind;
	return la.kind == Tokens.Else && peek == Tokens.If;
}

/*
	True if the next token is goto and this one is
	followed by minus ("-") (this is allowd in in
	error clauses)
*/
bool IsNegativeLabelName()
{
	int peek = Peek(1).kind;
	return la.kind == Tokens.GoTo && peek == Tokens.Minus;
}

/*
	True if the next statement is a "Resume next" statement
*/
bool IsResumeNext()
{
	int peek = Peek(1).kind;
	return la.kind == Tokens.Resume && peek == Tokens.Next;
}

/*
	True, if ident/literal integer is followed by ":"
*/
bool IsLabel()
{
	return (la.kind == Tokens.Identifier || la.kind == Tokens.LiteralInteger)
			&& Peek(1).kind == Tokens.Colon;
}

bool IsNotStatementSeparator()
{
	return la.kind == Tokens.Colon && Peek(1).kind == Tokens.EOL;
}

bool IsAssignment ()
{
	return IdentAndAsgn();
}

bool IsMustOverride(Modifiers m)
{
	return m.Contains(Modifier.Abstract);
}

TypeReferenceExpression GetTypeReferenceExpression(Expression expr, List<TypeReference> genericTypes)
{
	TypeReferenceExpression	tre = expr as TypeReferenceExpression;
	if (tre != null) {
		return new TypeReferenceExpression(new TypeReference(tre.TypeReference.Type, tre.TypeReference.PointerNestingLevel, tre.TypeReference.RankSpecifier, genericTypes));
	}
	StringBuilder b = new StringBuilder();
	if (!WriteFullTypeName(b, expr)) {
		// there is some TypeReferenceExpression hidden in the expression
		while (expr is FieldReferenceExpression) {
			expr = ((FieldReferenceExpression)expr).TargetObject;
		}
		tre = expr as TypeReferenceExpression;
		if (tre != null) {
			TypeReference typeRef = tre.TypeReference;
			if (typeRef.GenericTypes.Count == 0) {
				typeRef = typeRef.Clone();
				typeRef.Type += "." + b.ToString();
				typeRef.GenericTypes.AddRange(genericTypes);
			} else {
				typeRef = new InnerClassTypeReference(typeRef, b.ToString(), genericTypes);
			}
			return new TypeReferenceExpression(typeRef);
		}
	}
	return new TypeReferenceExpression(new TypeReference(b.ToString(), 0, null, genericTypes));
}

/* Writes the type name represented through the expression into the string builder. */
/* Returns true when the expression was converted successfully, returns false when */
/* There was an unknown expression (e.g. TypeReferenceExpression) in it */
bool WriteFullTypeName(StringBuilder b, Expression expr)
{
	FieldReferenceExpression fre = expr as FieldReferenceExpression;
	if (fre != null) {
		bool result = WriteFullTypeName(b, fre.TargetObject);
		if (b.Length > 0) b.Append('.');
		b.Append(fre.FieldName);
		return result;
	} else if (expr is IdentifierExpression) {
		b.Append(((IdentifierExpression)expr).Identifier);
		return true;
	} else {
		return false;
	}
}

/*
	True, if lookahead is a local attribute target specifier,
	i.e. one of "event", "return", "field", "method",
	"module", "param", "property", or "type"
*/
bool IsLocalAttrTarget() {
	// TODO
	return false;
}

/* START AUTOGENERATED TOKENS SECTION */


/*

*/

	void VBNET() {

#line  479 "VBNET.ATG" 
		compilationUnit = new CompilationUnit();
		withStatements = new Stack();
		
		while (la.kind == 1) {
			lexer.NextToken();
		}
		while (la.kind == 136) {
			OptionStmt();
		}
		while (la.kind == 108) {
			ImportsStmt();
		}
		while (
#line  485 "VBNET.ATG" 
IsGlobalAttrTarget()) {
			GlobalAttributeSection();
		}
		while (StartOf(1)) {
			NamespaceMemberDecl();
		}
		Expect(0);
	}

	void OptionStmt() {

#line  490 "VBNET.ATG" 
		INode node = null; bool val = true; 
		Expect(136);

#line  491 "VBNET.ATG" 
		Point startPos = t.Location; 
		if (la.kind == 95) {
			lexer.NextToken();
			if (la.kind == 134 || la.kind == 135) {
				OptionValue(
#line  493 "VBNET.ATG" 
ref val);
			}

#line  494 "VBNET.ATG" 
			node = new OptionDeclaration(OptionType.Explicit, val); 
		} else if (la.kind == 165) {
			lexer.NextToken();
			if (la.kind == 134 || la.kind == 135) {
				OptionValue(
#line  496 "VBNET.ATG" 
ref val);
			}

#line  497 "VBNET.ATG" 
			node = new OptionDeclaration(OptionType.Strict, val); 
		} else if (la.kind == 70) {
			lexer.NextToken();
			if (la.kind == 51) {
				lexer.NextToken();

#line  499 "VBNET.ATG" 
				node = new OptionDeclaration(OptionType.CompareBinary, val); 
			} else if (la.kind == 170) {
				lexer.NextToken();

#line  500 "VBNET.ATG" 
				node = new OptionDeclaration(OptionType.CompareText, val); 
			} else SynErr(207);
		} else SynErr(208);
		EndOfStmt();

#line  505 "VBNET.ATG" 
		if (node != null) {
		node.StartLocation = startPos;
		node.EndLocation   = t.Location;
		compilationUnit.AddChild(node);
		}
		
	}

	void ImportsStmt() {

#line  528 "VBNET.ATG" 
		List<Using> usings = new List<Using>();
		
		Expect(108);

#line  532 "VBNET.ATG" 
		Point startPos = t.Location;
		Using u;
		
		ImportClause(
#line  535 "VBNET.ATG" 
out u);

#line  535 "VBNET.ATG" 
		usings.Add(u); 
		while (la.kind == 12) {
			lexer.NextToken();
			ImportClause(
#line  537 "VBNET.ATG" 
out u);

#line  537 "VBNET.ATG" 
			usings.Add(u); 
		}
		EndOfStmt();

#line  541 "VBNET.ATG" 
		UsingDeclaration usingDeclaration = new UsingDeclaration(usings);
		usingDeclaration.StartLocation = startPos;
		usingDeclaration.EndLocation   = t.Location;
		compilationUnit.AddChild(usingDeclaration);
		
	}

	void GlobalAttributeSection() {

#line  2128 "VBNET.ATG" 
		Point startPos = t.Location; 
		Expect(27);
		if (la.kind == 49) {
			lexer.NextToken();
		} else if (la.kind == 121) {
			lexer.NextToken();
		} else SynErr(209);

#line  2130 "VBNET.ATG" 
		string attributeTarget = t.val.ToLower(System.Globalization.CultureInfo.InvariantCulture);
		List<ASTAttribute> attributes = new List<ASTAttribute>();
		ASTAttribute attribute;
		
		Expect(13);
		Attribute(
#line  2134 "VBNET.ATG" 
out attribute);

#line  2134 "VBNET.ATG" 
		attributes.Add(attribute); 
		while (
#line  2135 "VBNET.ATG" 
NotFinalComma()) {
			if (la.kind == 12) {
				lexer.NextToken();
				if (la.kind == 49) {
					lexer.NextToken();
				} else if (la.kind == 121) {
					lexer.NextToken();
				} else SynErr(210);
				Expect(13);
			}
			Attribute(
#line  2135 "VBNET.ATG" 
out attribute);

#line  2135 "VBNET.ATG" 
			attributes.Add(attribute); 
		}
		if (la.kind == 12) {
			lexer.NextToken();
		}
		Expect(26);
		EndOfStmt();

#line  2140 "VBNET.ATG" 
		AttributeSection section = new AttributeSection(attributeTarget, attributes);
		section.StartLocation = startPos;
		section.EndLocation = t.EndLocation;
		compilationUnit.AddChild(section);
		
	}

	void NamespaceMemberDecl() {

#line  570 "VBNET.ATG" 
		Modifiers m = new Modifiers();
		AttributeSection section;
		List<AttributeSection> attributes = new List<AttributeSection>();
		string qualident;
		
		if (la.kind == 126) {
			lexer.NextToken();

#line  577 "VBNET.ATG" 
			Point startPos = t.Location;
			
			Qualident(
#line  579 "VBNET.ATG" 
out qualident);

#line  581 "VBNET.ATG" 
			INode node =  new NamespaceDeclaration(qualident);
			node.StartLocation = startPos;
			compilationUnit.AddChild(node);
			compilationUnit.BlockStart(node);
			
			Expect(1);
			NamespaceBody();

#line  589 "VBNET.ATG" 
			node.EndLocation = t.Location;
			compilationUnit.BlockEnd();
			
		} else if (StartOf(2)) {
			while (la.kind == 27) {
				AttributeSection(
#line  593 "VBNET.ATG" 
out section);

#line  593 "VBNET.ATG" 
				attributes.Add(section); 
			}
			while (StartOf(3)) {
				TypeModifier(
#line  594 "VBNET.ATG" 
m);
			}
			NonModuleDeclaration(
#line  594 "VBNET.ATG" 
m, attributes);
		} else SynErr(211);
	}

	void OptionValue(
#line  513 "VBNET.ATG" 
ref bool val) {
		if (la.kind == 135) {
			lexer.NextToken();

#line  515 "VBNET.ATG" 
			val = true; 
		} else if (la.kind == 134) {
			lexer.NextToken();

#line  517 "VBNET.ATG" 
			val = false; 
		} else SynErr(212);
	}

	void EndOfStmt() {
		if (la.kind == 1) {
			lexer.NextToken();
		} else if (la.kind == 13) {
			lexer.NextToken();
			if (la.kind == 1) {
				lexer.NextToken();
			}
		} else SynErr(213);
	}

	void ImportClause(
#line  548 "VBNET.ATG" 
out Using u) {

#line  550 "VBNET.ATG" 
		string qualident  = null;
		TypeReference aliasedType = null;
		u = null;
		
		Qualident(
#line  554 "VBNET.ATG" 
out qualident);
		if (la.kind == 11) {
			lexer.NextToken();
			TypeName(
#line  555 "VBNET.ATG" 
out aliasedType);
		}

#line  557 "VBNET.ATG" 
		if (qualident != null && qualident.Length > 0) {
		if (aliasedType != null) {
			u = new Using(qualident, aliasedType);
		} else {
			u = new Using(qualident);
		}
		}
		
	}

	void Qualident(
#line  2851 "VBNET.ATG" 
out string qualident) {

#line  2853 "VBNET.ATG" 
		string name;
		qualidentBuilder.Length = 0; 
		
		Identifier();

#line  2857 "VBNET.ATG" 
		qualidentBuilder.Append(t.val); 
		while (
#line  2858 "VBNET.ATG" 
DotAndIdentOrKw()) {
			Expect(10);
			IdentifierOrKeyword(
#line  2858 "VBNET.ATG" 
out name);

#line  2858 "VBNET.ATG" 
			qualidentBuilder.Append('.'); qualidentBuilder.Append(name); 
		}

#line  2860 "VBNET.ATG" 
		qualident = qualidentBuilder.ToString(); 
	}

	void TypeName(
#line  2021 "VBNET.ATG" 
out TypeReference typeref) {

#line  2022 "VBNET.ATG" 
		ArrayList rank = null; 
		NonArrayTypeName(
#line  2024 "VBNET.ATG" 
out typeref, false);
		ArrayTypeModifiers(
#line  2025 "VBNET.ATG" 
out rank);

#line  2026 "VBNET.ATG" 
		if (rank != null && typeref != null) {
		typeref.RankSpecifier = (int[])rank.ToArray(typeof(int));
		}
		
	}

	void NamespaceBody() {
		while (StartOf(1)) {
			NamespaceMemberDecl();
		}
		Expect(88);
		Expect(126);
		Expect(1);
	}

	void AttributeSection(
#line  2197 "VBNET.ATG" 
out AttributeSection section) {

#line  2199 "VBNET.ATG" 
		string attributeTarget = "";List<ASTAttribute> attributes = new List<ASTAttribute>();
		ASTAttribute attribute;
		
		
		Expect(27);

#line  2203 "VBNET.ATG" 
		Point startPos = t.Location; 
		if (
#line  2204 "VBNET.ATG" 
IsLocalAttrTarget()) {
			if (la.kind == 93) {
				lexer.NextToken();

#line  2205 "VBNET.ATG" 
				attributeTarget = "event";
			} else if (la.kind == 155) {
				lexer.NextToken();

#line  2206 "VBNET.ATG" 
				attributeTarget = "return";
			} else {
				Identifier();

#line  2209 "VBNET.ATG" 
				string val = t.val.ToLower(System.Globalization.CultureInfo.InvariantCulture);
				if (val != "field"	|| val != "method" ||
					val != "module" || val != "param"  ||
					val != "property" || val != "type")
				Error("attribute target specifier (event, return, field," +
						"method, module, param, property, or type) expected");
				attributeTarget = t.val;
				
			}
			Expect(13);
		}
		Attribute(
#line  2219 "VBNET.ATG" 
out attribute);

#line  2219 "VBNET.ATG" 
		attributes.Add(attribute); 
		while (
#line  2220 "VBNET.ATG" 
NotFinalComma()) {
			Expect(12);
			Attribute(
#line  2220 "VBNET.ATG" 
out attribute);

#line  2220 "VBNET.ATG" 
			attributes.Add(attribute); 
		}
		if (la.kind == 12) {
			lexer.NextToken();
		}
		Expect(26);

#line  2224 "VBNET.ATG" 
		section = new AttributeSection(attributeTarget, attributes);
		section.StartLocation = startPos;
		section.EndLocation = t.EndLocation;
		
	}

	void TypeModifier(
#line  2906 "VBNET.ATG" 
Modifiers m) {
		switch (la.kind) {
		case 149: {
			lexer.NextToken();

#line  2907 "VBNET.ATG" 
			m.Add(Modifier.Public, t.Location); 
			break;
		}
		case 148: {
			lexer.NextToken();

#line  2908 "VBNET.ATG" 
			m.Add(Modifier.Protected, t.Location); 
			break;
		}
		case 99: {
			lexer.NextToken();

#line  2909 "VBNET.ATG" 
			m.Add(Modifier.Internal, t.Location); 
			break;
		}
		case 146: {
			lexer.NextToken();

#line  2910 "VBNET.ATG" 
			m.Add(Modifier.Private, t.Location); 
			break;
		}
		case 159: {
			lexer.NextToken();

#line  2911 "VBNET.ATG" 
			m.Add(Modifier.Static, t.Location); 
			break;
		}
		case 158: {
			lexer.NextToken();

#line  2912 "VBNET.ATG" 
			m.Add(Modifier.New, t.Location); 
			break;
		}
		case 122: {
			lexer.NextToken();

#line  2913 "VBNET.ATG" 
			m.Add(Modifier.Abstract, t.Location); 
			break;
		}
		case 131: {
			lexer.NextToken();

#line  2914 "VBNET.ATG" 
			m.Add(Modifier.Sealed, t.Location); 
			break;
		}
		case 204: {
			lexer.NextToken();

#line  2915 "VBNET.ATG" 
			m.Add(Modifier.Partial, t.Location); 
			break;
		}
		default: SynErr(214); break;
		}
	}

	void NonModuleDeclaration(
#line  645 "VBNET.ATG" 
Modifiers m, List<AttributeSection> attributes) {

#line  647 "VBNET.ATG" 
		string name = null;
		TypeReference typeRef = null;
		List<TypeReference> baseInterfaces = null;
		
		switch (la.kind) {
		case 67: {

#line  651 "VBNET.ATG" 
			m.Check(Modifier.Classes); 
			lexer.NextToken();

#line  654 "VBNET.ATG" 
			TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
			newType.StartLocation = t.Location;
			compilationUnit.AddChild(newType);
			compilationUnit.BlockStart(newType);
			
			newType.Type       = ClassType.Class;
			
			Identifier();

#line  661 "VBNET.ATG" 
			newType.Name = t.val; 
			TypeParameterList(
#line  662 "VBNET.ATG" 
newType.Templates);
			EndOfStmt();
			if (la.kind == 110) {
				ClassBaseType(
#line  664 "VBNET.ATG" 
out typeRef);

#line  664 "VBNET.ATG" 
				newType.BaseTypes.Add(typeRef); 
			}
			while (la.kind == 107) {
				TypeImplementsClause(
#line  665 "VBNET.ATG" 
out baseInterfaces);

#line  665 "VBNET.ATG" 
				newType.BaseTypes.AddRange(baseInterfaces); 
			}
			ClassBody(
#line  666 "VBNET.ATG" 
newType);

#line  668 "VBNET.ATG" 
			compilationUnit.BlockEnd();
			
			break;
		}
		case 121: {
			lexer.NextToken();

#line  672 "VBNET.ATG" 
			m.Check(Modifier.VBModules);
			TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
			compilationUnit.AddChild(newType);
			compilationUnit.BlockStart(newType);
			newType.StartLocation = m.GetDeclarationLocation(t.Location);
			newType.Type = ClassType.Module;
			
			Identifier();

#line  679 "VBNET.ATG" 
			newType.Name = t.val; 
			Expect(1);
			ModuleBody(
#line  681 "VBNET.ATG" 
newType);

#line  683 "VBNET.ATG" 
			compilationUnit.BlockEnd();
			
			break;
		}
		case 167: {
			lexer.NextToken();

#line  687 "VBNET.ATG" 
			m.Check(Modifier.VBStructures);
			TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
			compilationUnit.AddChild(newType);
			compilationUnit.BlockStart(newType);
			newType.StartLocation = m.GetDeclarationLocation(t.Location);
			newType.Type = ClassType.Struct;
			
			Identifier();

#line  694 "VBNET.ATG" 
			newType.Name = t.val; 
			TypeParameterList(
#line  695 "VBNET.ATG" 
newType.Templates);
			Expect(1);
			while (la.kind == 107) {
				TypeImplementsClause(
#line  696 "VBNET.ATG" 
out baseInterfaces);

#line  696 "VBNET.ATG" 
				newType.BaseTypes.AddRange(baseInterfaces);
			}
			StructureBody(
#line  697 "VBNET.ATG" 
newType);

#line  699 "VBNET.ATG" 
			compilationUnit.BlockEnd();
			
			break;
		}
		case 90: {
			lexer.NextToken();

#line  704 "VBNET.ATG" 
			m.Check(Modifier.VBEnums);
			TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
			newType.StartLocation = m.GetDeclarationLocation(t.Location);
			compilationUnit.AddChild(newType);
			compilationUnit.BlockStart(newType);
			
			newType.Type = ClassType.Enum;
			
			Identifier();

#line  712 "VBNET.ATG" 
			newType.Name = t.val; 
			if (la.kind == 48) {
				lexer.NextToken();
				PrimitiveTypeName(
#line  713 "VBNET.ATG" 
out name);

#line  713 "VBNET.ATG" 
				newType.BaseTypes.Add(new TypeReference(name)); 
			}
			Expect(1);
			EnumBody(
#line  715 "VBNET.ATG" 
newType);

#line  717 "VBNET.ATG" 
			compilationUnit.BlockEnd();
			
			break;
		}
		case 112: {
			lexer.NextToken();

#line  722 "VBNET.ATG" 
			m.Check(Modifier.VBInterfacs);
			TypeDeclaration newType = new TypeDeclaration(m.Modifier, attributes);
			newType.StartLocation = m.GetDeclarationLocation(t.Location);
			compilationUnit.AddChild(newType);
			compilationUnit.BlockStart(newType);
			newType.Type = ClassType.Interface;
			
			Identifier();

#line  729 "VBNET.ATG" 
			newType.Name = t.val; 
			TypeParameterList(
#line  730 "VBNET.ATG" 
newType.Templates);
			EndOfStmt();
			while (la.kind == 110) {
				InterfaceBase(
#line  731 "VBNET.ATG" 
out baseInterfaces);

#line  731 "VBNET.ATG" 
				newType.BaseTypes.AddRange(baseInterfaces); 
			}
			InterfaceBody(
#line  732 "VBNET.ATG" 
newType);

#line  734 "VBNET.ATG" 
			compilationUnit.BlockEnd();
			
			break;
		}
		case 80: {
			lexer.NextToken();

#line  739 "VBNET.ATG" 
			m.Check(Modifier.VBDelegates);
			DelegateDeclaration delegateDeclr = new DelegateDeclaration(m.Modifier, attributes);
			delegateDeclr.ReturnType = new TypeReference("", "System.Void");
			delegateDeclr.StartLocation = m.GetDeclarationLocation(t.Location);
			List<ParameterDeclarationExpression> p = new List<ParameterDeclarationExpression>();
			
			if (la.kind == 168) {
				lexer.NextToken();
				Identifier();

#line  746 "VBNET.ATG" 
				delegateDeclr.Name = t.val; 
				TypeParameterList(
#line  747 "VBNET.ATG" 
delegateDeclr.Templates);
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  748 "VBNET.ATG" 
p);
					}
					Expect(25);

#line  748 "VBNET.ATG" 
					delegateDeclr.Parameters = p; 
				}
			} else if (la.kind == 100) {
				lexer.NextToken();
				Identifier();

#line  750 "VBNET.ATG" 
				delegateDeclr.Name = t.val; 
				TypeParameterList(
#line  751 "VBNET.ATG" 
delegateDeclr.Templates);
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  752 "VBNET.ATG" 
p);
					}
					Expect(25);

#line  752 "VBNET.ATG" 
					delegateDeclr.Parameters = p; 
				}
				if (la.kind == 48) {
					lexer.NextToken();

#line  753 "VBNET.ATG" 
					TypeReference type; 
					TypeName(
#line  753 "VBNET.ATG" 
out type);

#line  753 "VBNET.ATG" 
					delegateDeclr.ReturnType = type; 
				}
			} else SynErr(215);

#line  755 "VBNET.ATG" 
			delegateDeclr.EndLocation = t.EndLocation; 
			Expect(1);

#line  758 "VBNET.ATG" 
			compilationUnit.AddChild(delegateDeclr);
			
			break;
		}
		default: SynErr(216); break;
		}
	}

	void TypeParameterList(
#line  598 "VBNET.ATG" 
List<TemplateDefinition> templates) {

#line  600 "VBNET.ATG" 
		TemplateDefinition template;
		
		if (
#line  603 "VBNET.ATG" 
la.kind == Tokens.OpenParenthesis && Peek(1).kind == Tokens.Of) {
			lexer.NextToken();
			Expect(201);
			TypeParameter(
#line  604 "VBNET.ATG" 
out template);

#line  606 "VBNET.ATG" 
			if (template != null) templates.Add(template);
			
			while (la.kind == 12) {
				lexer.NextToken();
				TypeParameter(
#line  609 "VBNET.ATG" 
out template);

#line  611 "VBNET.ATG" 
				if (template != null) templates.Add(template);
				
			}
			Expect(25);
		}
	}

	void TypeParameter(
#line  619 "VBNET.ATG" 
out TemplateDefinition template) {
		Identifier();

#line  621 "VBNET.ATG" 
		template = new TemplateDefinition(t.val, null); 
		if (la.kind == 48) {
			TypeParameterConstraints(
#line  622 "VBNET.ATG" 
template);
		}
	}

	void Identifier() {
		if (la.kind == 2) {
			lexer.NextToken();
		} else if (la.kind == 170) {
			lexer.NextToken();
		} else if (la.kind == 51) {
			lexer.NextToken();
		} else if (la.kind == 70) {
			lexer.NextToken();
		} else SynErr(217);
	}

	void TypeParameterConstraints(
#line  626 "VBNET.ATG" 
TemplateDefinition template) {

#line  628 "VBNET.ATG" 
		TypeReference constraint;
		
		Expect(48);
		if (la.kind == 22) {
			lexer.NextToken();
			TypeName(
#line  634 "VBNET.ATG" 
out constraint);

#line  634 "VBNET.ATG" 
			if (constraint != null) { template.Bases.Add(constraint); } 
			while (la.kind == 12) {
				lexer.NextToken();
				TypeName(
#line  637 "VBNET.ATG" 
out constraint);

#line  637 "VBNET.ATG" 
				if (constraint != null) { template.Bases.Add(constraint); } 
			}
			Expect(23);
		} else if (StartOf(5)) {
			TypeName(
#line  640 "VBNET.ATG" 
out constraint);

#line  640 "VBNET.ATG" 
			if (constraint != null) { template.Bases.Add(constraint); } 
		} else SynErr(218);
	}

	void ClassBaseType(
#line  935 "VBNET.ATG" 
out TypeReference typeRef) {

#line  937 "VBNET.ATG" 
		typeRef = null;
		
		Expect(110);
		TypeName(
#line  940 "VBNET.ATG" 
out typeRef);
		EndOfStmt();
	}

	void TypeImplementsClause(
#line  1649 "VBNET.ATG" 
out List<TypeReference> baseInterfaces) {

#line  1651 "VBNET.ATG" 
		baseInterfaces = new List<TypeReference>();
		TypeReference type = null;
		
		Expect(107);
		TypeName(
#line  1654 "VBNET.ATG" 
out type);

#line  1656 "VBNET.ATG" 
		baseInterfaces.Add(type);
		
		while (la.kind == 12) {
			lexer.NextToken();
			TypeName(
#line  1659 "VBNET.ATG" 
out type);

#line  1660 "VBNET.ATG" 
			baseInterfaces.Add(type); 
		}
		EndOfStmt();
	}

	void ClassBody(
#line  768 "VBNET.ATG" 
TypeDeclaration newType) {

#line  769 "VBNET.ATG" 
		AttributeSection section; 
		while (StartOf(6)) {

#line  771 "VBNET.ATG" 
			List<AttributeSection> attributes = new List<AttributeSection>();
			Modifiers m = new Modifiers();
			
			while (la.kind == 27) {
				AttributeSection(
#line  774 "VBNET.ATG" 
out section);

#line  774 "VBNET.ATG" 
				attributes.Add(section); 
			}
			while (StartOf(7)) {
				MemberModifier(
#line  775 "VBNET.ATG" 
m);
			}
			ClassMemberDecl(
#line  776 "VBNET.ATG" 
m, attributes);
		}
		Expect(88);
		Expect(67);

#line  778 "VBNET.ATG" 
		newType.EndLocation = t.EndLocation; 
		Expect(1);
	}

	void ModuleBody(
#line  797 "VBNET.ATG" 
TypeDeclaration newType) {

#line  798 "VBNET.ATG" 
		AttributeSection section; 
		while (StartOf(6)) {

#line  800 "VBNET.ATG" 
			List<AttributeSection> attributes = new List<AttributeSection>();
			Modifiers m = new Modifiers();
			
			while (la.kind == 27) {
				AttributeSection(
#line  803 "VBNET.ATG" 
out section);

#line  803 "VBNET.ATG" 
				attributes.Add(section); 
			}
			while (StartOf(7)) {
				MemberModifier(
#line  804 "VBNET.ATG" 
m);
			}
			ClassMemberDecl(
#line  805 "VBNET.ATG" 
m, attributes);
		}
		Expect(88);
		Expect(121);

#line  807 "VBNET.ATG" 
		newType.EndLocation = t.EndLocation; 
		Expect(1);
	}

	void StructureBody(
#line  782 "VBNET.ATG" 
TypeDeclaration newType) {

#line  783 "VBNET.ATG" 
		AttributeSection section; 
		while (StartOf(6)) {

#line  785 "VBNET.ATG" 
			List<AttributeSection> attributes = new List<AttributeSection>();
			Modifiers m = new Modifiers();
			
			while (la.kind == 27) {
				AttributeSection(
#line  788 "VBNET.ATG" 
out section);

#line  788 "VBNET.ATG" 
				attributes.Add(section); 
			}
			while (StartOf(7)) {
				MemberModifier(
#line  789 "VBNET.ATG" 
m);
			}
			StructureMemberDecl(
#line  790 "VBNET.ATG" 
m, attributes);
		}
		Expect(88);
		Expect(167);

#line  792 "VBNET.ATG" 
		newType.EndLocation = t.EndLocation; 
		Expect(1);
	}

	void PrimitiveTypeName(
#line  2880 "VBNET.ATG" 
out string type) {

#line  2881 "VBNET.ATG" 
		type = String.Empty; 
		switch (la.kind) {
		case 52: {
			lexer.NextToken();

#line  2882 "VBNET.ATG" 
			type = "Boolean"; 
			break;
		}
		case 76: {
			lexer.NextToken();

#line  2883 "VBNET.ATG" 
			type = "Date"; 
			break;
		}
		case 65: {
			lexer.NextToken();

#line  2884 "VBNET.ATG" 
			type = "Char"; 
			break;
		}
		case 166: {
			lexer.NextToken();

#line  2885 "VBNET.ATG" 
			type = "String"; 
			break;
		}
		case 77: {
			lexer.NextToken();

#line  2886 "VBNET.ATG" 
			type = "Decimal"; 
			break;
		}
		case 54: {
			lexer.NextToken();

#line  2887 "VBNET.ATG" 
			type = "Byte"; 
			break;
		}
		case 160: {
			lexer.NextToken();

#line  2888 "VBNET.ATG" 
			type = "Short"; 
			break;
		}
		case 111: {
			lexer.NextToken();

#line  2889 "VBNET.ATG" 
			type = "Integer"; 
			break;
		}
		case 117: {
			lexer.NextToken();

#line  2890 "VBNET.ATG" 
			type = "Long"; 
			break;
		}
		case 161: {
			lexer.NextToken();

#line  2891 "VBNET.ATG" 
			type = "Single"; 
			break;
		}
		case 84: {
			lexer.NextToken();

#line  2892 "VBNET.ATG" 
			type = "Double"; 
			break;
		}
		case 192: {
			lexer.NextToken();

#line  2893 "VBNET.ATG" 
			type = "UInteger"; 
			break;
		}
		case 193: {
			lexer.NextToken();

#line  2894 "VBNET.ATG" 
			type = "ULong"; 
			break;
		}
		case 194: {
			lexer.NextToken();

#line  2895 "VBNET.ATG" 
			type = "UShort"; 
			break;
		}
		case 191: {
			lexer.NextToken();

#line  2896 "VBNET.ATG" 
			type = "SByte"; 
			break;
		}
		default: SynErr(219); break;
		}
	}

	void EnumBody(
#line  811 "VBNET.ATG" 
TypeDeclaration newType) {

#line  812 "VBNET.ATG" 
		FieldDeclaration f; 
		while (StartOf(8)) {
			EnumMemberDecl(
#line  814 "VBNET.ATG" 
out f);

#line  814 "VBNET.ATG" 
			compilationUnit.AddChild(f); 
		}
		Expect(88);
		Expect(90);

#line  816 "VBNET.ATG" 
		newType.EndLocation = t.EndLocation; 
		Expect(1);
	}

	void InterfaceBase(
#line  1634 "VBNET.ATG" 
out List<TypeReference> bases) {

#line  1636 "VBNET.ATG" 
		TypeReference type;
		bases = new List<TypeReference>();
		
		Expect(110);
		TypeName(
#line  1640 "VBNET.ATG" 
out type);

#line  1640 "VBNET.ATG" 
		bases.Add(type); 
		while (la.kind == 12) {
			lexer.NextToken();
			TypeName(
#line  1643 "VBNET.ATG" 
out type);

#line  1643 "VBNET.ATG" 
			bases.Add(type); 
		}
		Expect(1);
	}

	void InterfaceBody(
#line  820 "VBNET.ATG" 
TypeDeclaration newType) {
		while (StartOf(9)) {
			InterfaceMemberDecl();
		}
		Expect(88);
		Expect(112);

#line  822 "VBNET.ATG" 
		newType.EndLocation = t.EndLocation; 
		Expect(1);
	}

	void FormalParameterList(
#line  2231 "VBNET.ATG" 
List<ParameterDeclarationExpression> parameter) {

#line  2233 "VBNET.ATG" 
		ParameterDeclarationExpression p;
		AttributeSection section;
		List<AttributeSection> attributes = new List<AttributeSection>();
		
		while (la.kind == 27) {
			AttributeSection(
#line  2237 "VBNET.ATG" 
out section);

#line  2237 "VBNET.ATG" 
			attributes.Add(section); 
		}
		FormalParameter(
#line  2239 "VBNET.ATG" 
out p);

#line  2241 "VBNET.ATG" 
		bool paramsFound = false;
		p.Attributes = attributes;
		parameter.Add(p);
		
		while (la.kind == 12) {
			lexer.NextToken();

#line  2246 "VBNET.ATG" 
			if (paramsFound) Error("params array must be at end of parameter list"); 
			while (la.kind == 27) {
				AttributeSection(
#line  2247 "VBNET.ATG" 
out section);

#line  2247 "VBNET.ATG" 
				attributes.Add(section); 
			}
			FormalParameter(
#line  2249 "VBNET.ATG" 
out p);

#line  2249 "VBNET.ATG" 
			p.Attributes = attributes; parameter.Add(p); 
		}
	}

	void MemberModifier(
#line  2918 "VBNET.ATG" 
Modifiers m) {
		switch (la.kind) {
		case 122: {
			lexer.NextToken();

#line  2919 "VBNET.ATG" 
			m.Add(Modifier.Abstract, t.Location);
			break;
		}
		case 79: {
			lexer.NextToken();

#line  2920 "VBNET.ATG" 
			m.Add(Modifier.Default, t.Location);
			break;
		}
		case 99: {
			lexer.NextToken();

#line  2921 "VBNET.ATG" 
			m.Add(Modifier.Internal, t.Location);
			break;
		}
		case 158: {
			lexer.NextToken();

#line  2922 "VBNET.ATG" 
			m.Add(Modifier.New, t.Location);
			break;
		}
		case 143: {
			lexer.NextToken();

#line  2923 "VBNET.ATG" 
			m.Add(Modifier.Override, t.Location);
			break;
		}
		case 123: {
			lexer.NextToken();

#line  2924 "VBNET.ATG" 
			m.Add(Modifier.Abstract, t.Location);
			break;
		}
		case 146: {
			lexer.NextToken();

#line  2925 "VBNET.ATG" 
			m.Add(Modifier.Private, t.Location);
			break;
		}
		case 148: {
			lexer.NextToken();

#line  2926 "VBNET.ATG" 
			m.Add(Modifier.Protected, t.Location);
			break;
		}
		case 149: {
			lexer.NextToken();

#line  2927 "VBNET.ATG" 
			m.Add(Modifier.Public, t.Location);
			break;
		}
		case 131: {
			lexer.NextToken();

#line  2928 "VBNET.ATG" 
			m.Add(Modifier.Sealed, t.Location);
			break;
		}
		case 132: {
			lexer.NextToken();

#line  2929 "VBNET.ATG" 
			m.Add(Modifier.Sealed, t.Location);
			break;
		}
		case 159: {
			lexer.NextToken();

#line  2930 "VBNET.ATG" 
			m.Add(Modifier.Static, t.Location);
			break;
		}
		case 141: {
			lexer.NextToken();

#line  2931 "VBNET.ATG" 
			m.Add(Modifier.Virtual, t.Location);
			break;
		}
		case 140: {
			lexer.NextToken();

#line  2932 "VBNET.ATG" 
			m.Add(Modifier.Overloads, t.Location);
			break;
		}
		case 151: {
			lexer.NextToken();

#line  2933 "VBNET.ATG" 
			
			break;
		}
		case 185: {
			lexer.NextToken();

#line  2934 "VBNET.ATG" 
			
			break;
		}
		case 184: {
			lexer.NextToken();

#line  2935 "VBNET.ATG" 
			m.Add(Modifier.WithEvents, t.Location);
			break;
		}
		case 81: {
			lexer.NextToken();

#line  2936 "VBNET.ATG" 
			m.Add(Modifier.Dim, t.Location);
			break;
		}
		case 203: {
			lexer.NextToken();

#line  2937 "VBNET.ATG" 
			m.Add(Modifier.Widening, t.Location);
			break;
		}
		case 202: {
			lexer.NextToken();

#line  2938 "VBNET.ATG" 
			m.Add(Modifier.Narrowing, t.Location);
			break;
		}
		default: SynErr(220); break;
		}
	}

	void ClassMemberDecl(
#line  931 "VBNET.ATG" 
Modifiers m, List<AttributeSection> attributes) {
		StructureMemberDecl(
#line  932 "VBNET.ATG" 
m, attributes);
	}

	void StructureMemberDecl(
#line  945 "VBNET.ATG" 
Modifiers m, List<AttributeSection> attributes) {

#line  947 "VBNET.ATG" 
		TypeReference type = null;
		List<ParameterDeclarationExpression> p = new List<ParameterDeclarationExpression>();
		Statement stmt = null;
		List<VariableDeclaration> variableDeclarators = new List<VariableDeclaration>();
		List<TemplateDefinition> templates = new List<TemplateDefinition>();
		
		switch (la.kind) {
		case 67: case 80: case 90: case 112: case 121: case 167: {
			NonModuleDeclaration(
#line  953 "VBNET.ATG" 
m, attributes);
			break;
		}
		case 168: {
			lexer.NextToken();

#line  957 "VBNET.ATG" 
			Point startPos = t.Location;
			
			if (StartOf(10)) {

#line  961 "VBNET.ATG" 
				string name = String.Empty;
				MethodDeclaration methodDeclaration; ArrayList handlesClause = null;
				List<InterfaceImplementation> implementsClause = null;
				
				Identifier();

#line  967 "VBNET.ATG" 
				name = t.val;
				m.Check(Modifier.VBMethods);
				
				TypeParameterList(
#line  970 "VBNET.ATG" 
templates);
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  971 "VBNET.ATG" 
p);
					}
					Expect(25);
				}
				if (la.kind == 105 || la.kind == 107) {
					if (la.kind == 107) {
						ImplementsClause(
#line  974 "VBNET.ATG" 
out implementsClause);
					} else {
						HandlesClause(
#line  976 "VBNET.ATG" 
out handlesClause);
					}
				}

#line  979 "VBNET.ATG" 
				Point endLocation = t.EndLocation; 
				Expect(1);
				if (
#line  983 "VBNET.ATG" 
IsMustOverride(m)) {

#line  985 "VBNET.ATG" 
					methodDeclaration = new MethodDeclaration(name, m.Modifier,  null, p, attributes);
					methodDeclaration.StartLocation = m.GetDeclarationLocation(startPos);
					methodDeclaration.EndLocation   = endLocation;
					methodDeclaration.TypeReference = new TypeReference("", "System.Void");
					
					methodDeclaration.Templates = templates;
					methodDeclaration.HandlesClause = handlesClause;
					methodDeclaration.InterfaceImplementations = implementsClause;
					
					compilationUnit.AddChild(methodDeclaration);
					
				} else if (StartOf(11)) {

#line  998 "VBNET.ATG" 
					methodDeclaration = new MethodDeclaration(name, m.Modifier,  null, p, attributes);
					methodDeclaration.StartLocation = m.GetDeclarationLocation(startPos);
					methodDeclaration.EndLocation   = endLocation;
					methodDeclaration.TypeReference = new TypeReference("", "System.Void");
					
					methodDeclaration.Templates = templates;
					methodDeclaration.HandlesClause = handlesClause;
					methodDeclaration.InterfaceImplementations = implementsClause;
					
					compilationUnit.AddChild(methodDeclaration);
					compilationUnit.BlockStart(methodDeclaration);
					
					Block(
#line  1010 "VBNET.ATG" 
out stmt);

#line  1012 "VBNET.ATG" 
					compilationUnit.BlockEnd();
					methodDeclaration.Body  = (BlockStatement)stmt;
					
					Expect(88);
					Expect(168);

#line  1015 "VBNET.ATG" 
					methodDeclaration.Body.EndLocation = t.EndLocation; 
					Expect(1);
				} else SynErr(221);
			} else if (la.kind == 127) {
				lexer.NextToken();
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  1018 "VBNET.ATG" 
p);
					}
					Expect(25);
				}

#line  1019 "VBNET.ATG" 
				m.Check(Modifier.Constructors); 

#line  1020 "VBNET.ATG" 
				Point constructorEndLocation = t.EndLocation; 
				Expect(1);
				Block(
#line  1022 "VBNET.ATG" 
out stmt);
				Expect(88);
				Expect(168);

#line  1023 "VBNET.ATG" 
				Point endLocation = t.EndLocation; 
				Expect(1);

#line  1025 "VBNET.ATG" 
				ConstructorDeclaration cd = new ConstructorDeclaration("New", m.Modifier, p, attributes); 
				cd.StartLocation = m.GetDeclarationLocation(startPos);
				cd.EndLocation   = constructorEndLocation;
				cd.Body = (BlockStatement)stmt;
				cd.Body.EndLocation   = endLocation;
				compilationUnit.AddChild(cd);
				
			} else SynErr(222);
			break;
		}
		case 100: {
			lexer.NextToken();

#line  1037 "VBNET.ATG" 
			m.Check(Modifier.VBMethods);
			string name = String.Empty;
			Point startPos = t.Location;
			MethodDeclaration methodDeclaration;ArrayList handlesClause = null;
			List<InterfaceImplementation> implementsClause = null;
			AttributeSection returnTypeAttributeSection = null;
			
			Identifier();

#line  1044 "VBNET.ATG" 
			name = t.val; 
			TypeParameterList(
#line  1045 "VBNET.ATG" 
templates);
			if (la.kind == 24) {
				lexer.NextToken();
				if (StartOf(4)) {
					FormalParameterList(
#line  1046 "VBNET.ATG" 
p);
				}
				Expect(25);
			}
			if (la.kind == 48) {
				lexer.NextToken();
				while (la.kind == 27) {
					AttributeSection(
#line  1047 "VBNET.ATG" 
out returnTypeAttributeSection);
				}
				TypeName(
#line  1047 "VBNET.ATG" 
out type);
			}

#line  1049 "VBNET.ATG" 
			if(type == null) {
			type = new TypeReference("System.Object");
			}
			
			if (la.kind == 105 || la.kind == 107) {
				if (la.kind == 107) {
					ImplementsClause(
#line  1055 "VBNET.ATG" 
out implementsClause);
				} else {
					HandlesClause(
#line  1057 "VBNET.ATG" 
out handlesClause);
				}
			}
			Expect(1);
			if (
#line  1063 "VBNET.ATG" 
IsMustOverride(m)) {

#line  1065 "VBNET.ATG" 
				methodDeclaration = new MethodDeclaration(name, m.Modifier,  type, p, attributes);
				methodDeclaration.StartLocation = m.GetDeclarationLocation(startPos);
				methodDeclaration.EndLocation   = t.EndLocation;
				
				methodDeclaration.HandlesClause = handlesClause;
				methodDeclaration.Templates     = templates;
				methodDeclaration.InterfaceImplementations = implementsClause;
				methodDeclaration.ReturnTypeAttributeSection = returnTypeAttributeSection;
				compilationUnit.AddChild(methodDeclaration);
				
			} else if (StartOf(11)) {

#line  1077 "VBNET.ATG" 
				methodDeclaration = new MethodDeclaration(name, m.Modifier,  type, p, attributes);
				methodDeclaration.StartLocation = m.GetDeclarationLocation(startPos);
				methodDeclaration.EndLocation   = t.EndLocation;
				
				methodDeclaration.Templates     = templates;
				methodDeclaration.HandlesClause = handlesClause;
				methodDeclaration.InterfaceImplementations = implementsClause;
				methodDeclaration.ReturnTypeAttributeSection = returnTypeAttributeSection;
				
				compilationUnit.AddChild(methodDeclaration);
				compilationUnit.BlockStart(methodDeclaration);
				
				Block(
#line  1089 "VBNET.ATG" 
out stmt);

#line  1091 "VBNET.ATG" 
				compilationUnit.BlockEnd();
				methodDeclaration.Body  = (BlockStatement)stmt;
				
				Expect(88);
				Expect(100);

#line  1096 "VBNET.ATG" 
				methodDeclaration.Body.StartLocation = methodDeclaration.EndLocation;
				methodDeclaration.Body.EndLocation   = t.EndLocation;
				
				Expect(1);
			} else SynErr(223);
			break;
		}
		case 78: {
			lexer.NextToken();

#line  1105 "VBNET.ATG" 
			m.Check(Modifier.VBExternalMethods);
			Point startPos = t.Location;
			CharsetModifier charsetModifer = CharsetModifier.None;
			string library = String.Empty;
			string alias = null;
			string name = String.Empty;
			
			if (StartOf(12)) {
				Charset(
#line  1112 "VBNET.ATG" 
out charsetModifer);
			}
			if (la.kind == 168) {
				lexer.NextToken();
				Identifier();

#line  1115 "VBNET.ATG" 
				name = t.val; 
				Expect(115);
				Expect(3);

#line  1116 "VBNET.ATG" 
				library = t.val.ToString(); 
				if (la.kind == 44) {
					lexer.NextToken();
					Expect(3);

#line  1117 "VBNET.ATG" 
					alias = t.val.ToString(); 
				}
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  1118 "VBNET.ATG" 
p);
					}
					Expect(25);
				}
				Expect(1);

#line  1121 "VBNET.ATG" 
				DeclareDeclaration declareDeclaration = new DeclareDeclaration(name, m.Modifier, null, p, attributes, library, alias, charsetModifer);
				declareDeclaration.StartLocation = m.GetDeclarationLocation(startPos);
				declareDeclaration.EndLocation   = t.EndLocation;
				compilationUnit.AddChild(declareDeclaration);
				
			} else if (la.kind == 100) {
				lexer.NextToken();
				Identifier();

#line  1128 "VBNET.ATG" 
				name = t.val; 
				Expect(115);
				Expect(3);

#line  1129 "VBNET.ATG" 
				library = t.val; 
				if (la.kind == 44) {
					lexer.NextToken();
					Expect(3);

#line  1130 "VBNET.ATG" 
					alias = t.val; 
				}
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  1131 "VBNET.ATG" 
p);
					}
					Expect(25);
				}
				if (la.kind == 48) {
					lexer.NextToken();
					TypeName(
#line  1132 "VBNET.ATG" 
out type);
				}
				Expect(1);

#line  1135 "VBNET.ATG" 
				DeclareDeclaration declareDeclaration = new DeclareDeclaration(name, m.Modifier, type, p, attributes, library, alias, charsetModifer);
				declareDeclaration.StartLocation = m.GetDeclarationLocation(startPos);
				declareDeclaration.EndLocation   = t.EndLocation;
				compilationUnit.AddChild(declareDeclaration);
				
			} else SynErr(224);
			break;
		}
		case 93: {
			lexer.NextToken();

#line  1145 "VBNET.ATG" 
			m.Check(Modifier.VBEvents);
			Point startPos = t.Location;
			EventDeclaration eventDeclaration;
			string name = String.Empty;
			List<InterfaceImplementation> implementsClause = null;
			
			Identifier();

#line  1151 "VBNET.ATG" 
			name= t.val; 
			if (la.kind == 48) {
				lexer.NextToken();
				TypeName(
#line  1153 "VBNET.ATG" 
out type);
			} else if (la.kind == 1 || la.kind == 24 || la.kind == 107) {
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  1155 "VBNET.ATG" 
p);
					}
					Expect(25);
				}
			} else SynErr(225);
			if (la.kind == 107) {
				ImplementsClause(
#line  1157 "VBNET.ATG" 
out implementsClause);
			}

#line  1159 "VBNET.ATG" 
			eventDeclaration = new EventDeclaration(type, m.Modifier, p, attributes, name, implementsClause);
			eventDeclaration.StartLocation = m.GetDeclarationLocation(startPos);
			eventDeclaration.EndLocation = t.EndLocation;
			compilationUnit.AddChild(eventDeclaration);
			
			Expect(1);
			break;
		}
		case 2: case 51: case 70: case 170: {

#line  1166 "VBNET.ATG" 
			Point startPos = t.Location; 

#line  1168 "VBNET.ATG" 
			m.Check(Modifier.Fields);
			FieldDeclaration fd = new FieldDeclaration(attributes, type, m.Modifier);
			fd.StartLocation = m.GetDeclarationLocation(startPos); 
			
			VariableDeclarator(
#line  1172 "VBNET.ATG" 
variableDeclarators);
			while (la.kind == 12) {
				lexer.NextToken();
				VariableDeclarator(
#line  1173 "VBNET.ATG" 
variableDeclarators);
			}
			Expect(1);

#line  1176 "VBNET.ATG" 
			fd.EndLocation = t.EndLocation;
			fd.Fields = variableDeclarators;
			compilationUnit.AddChild(fd);
			
			break;
		}
		case 71: {

#line  1181 "VBNET.ATG" 
			m.Check(Modifier.Fields); 
			lexer.NextToken();

#line  1182 "VBNET.ATG" 
			m.Add(Modifier.Const, t.Location);  

#line  1184 "VBNET.ATG" 
			FieldDeclaration fd = new FieldDeclaration(attributes, type, m.Modifier);
			fd.StartLocation = m.GetDeclarationLocation(t.Location);
			List<VariableDeclaration> constantDeclarators = new List<VariableDeclaration>();
			
			ConstantDeclarator(
#line  1188 "VBNET.ATG" 
constantDeclarators);
			while (la.kind == 12) {
				lexer.NextToken();
				ConstantDeclarator(
#line  1189 "VBNET.ATG" 
constantDeclarators);
			}

#line  1191 "VBNET.ATG" 
			fd.Fields = constantDeclarators;
			fd.EndLocation = t.Location;
			
			Expect(1);

#line  1196 "VBNET.ATG" 
			fd.EndLocation = t.EndLocation;
			compilationUnit.AddChild(fd);
			
			break;
		}
		case 147: {
			lexer.NextToken();

#line  1202 "VBNET.ATG" 
			m.Check(Modifier.VBProperties);
			Point startPos = t.Location;
			List<InterfaceImplementation> implementsClause = null;
			
			Identifier();

#line  1206 "VBNET.ATG" 
			string propertyName = t.val; 
			if (la.kind == 24) {
				lexer.NextToken();
				if (StartOf(4)) {
					FormalParameterList(
#line  1207 "VBNET.ATG" 
p);
				}
				Expect(25);
			}
			if (la.kind == 48) {
				lexer.NextToken();
				TypeName(
#line  1208 "VBNET.ATG" 
out type);
			}

#line  1210 "VBNET.ATG" 
			if(type == null) {
			type = new TypeReference("System.Object");
			}
			
			if (la.kind == 107) {
				ImplementsClause(
#line  1214 "VBNET.ATG" 
out implementsClause);
			}
			Expect(1);
			if (
#line  1218 "VBNET.ATG" 
IsMustOverride(m)) {

#line  1220 "VBNET.ATG" 
				PropertyDeclaration pDecl = new PropertyDeclaration(propertyName, type, m.Modifier, attributes);
				pDecl.StartLocation = m.GetDeclarationLocation(startPos);
				pDecl.EndLocation   = t.Location;
				pDecl.TypeReference = type;
				pDecl.InterfaceImplementations = implementsClause;
				pDecl.Parameters = p;
				compilationUnit.AddChild(pDecl);
				
			} else if (la.kind == 27 || la.kind == 101 || la.kind == 157) {

#line  1230 "VBNET.ATG" 
				PropertyDeclaration pDecl = new PropertyDeclaration(propertyName, type, m.Modifier, attributes);
				pDecl.StartLocation = m.GetDeclarationLocation(startPos);
				pDecl.EndLocation   = t.Location;
				pDecl.BodyStart   = t.Location;
				pDecl.TypeReference = type;
				pDecl.InterfaceImplementations = implementsClause;
				pDecl.Parameters = p;
				PropertyGetRegion getRegion;
				PropertySetRegion setRegion;
				
				AccessorDecls(
#line  1240 "VBNET.ATG" 
out getRegion, out setRegion);
				Expect(88);
				Expect(147);
				Expect(1);

#line  1244 "VBNET.ATG" 
				pDecl.GetRegion = getRegion;
				pDecl.SetRegion = setRegion;
				pDecl.BodyEnd = t.EndLocation;
				compilationUnit.AddChild(pDecl);
				
			} else SynErr(226);
			break;
		}
		case 205: {
			lexer.NextToken();

#line  1251 "VBNET.ATG" 
			Point startPos = t.Location; 
			Expect(93);

#line  1253 "VBNET.ATG" 
			m.Check(Modifier.VBCustomEvents);
			EventAddRemoveRegion eventAccessorDeclaration;
			EventAddRegion addHandlerAccessorDeclaration = null;
			EventRemoveRegion removeHandlerAccessorDeclaration = null;
			EventRaiseRegion raiseEventAccessorDeclaration = null;
			List<InterfaceImplementation> implementsClause = null;
			
			Identifier();

#line  1260 "VBNET.ATG" 
			string customEventName = t.val; 
			Expect(48);
			TypeName(
#line  1261 "VBNET.ATG" 
out type);
			if (la.kind == 107) {
				ImplementsClause(
#line  1262 "VBNET.ATG" 
out implementsClause);
			}
			Expect(1);
			while (StartOf(13)) {
				EventAccessorDeclaration(
#line  1265 "VBNET.ATG" 
out eventAccessorDeclaration);

#line  1267 "VBNET.ATG" 
				if(eventAccessorDeclaration is EventAddRegion)
				{
					addHandlerAccessorDeclaration = (EventAddRegion)eventAccessorDeclaration;
				}
				else if(eventAccessorDeclaration is EventRemoveRegion)
				{
					removeHandlerAccessorDeclaration = (EventRemoveRegion)eventAccessorDeclaration;
				}
				else if(eventAccessorDeclaration is EventRaiseRegion)
				{
					raiseEventAccessorDeclaration = (EventRaiseRegion)eventAccessorDeclaration;
				}
				
			}
			Expect(88);
			Expect(93);
			Expect(1);

#line  1283 "VBNET.ATG" 
			if(addHandlerAccessorDeclaration == null)
			{
				Error("Need to provide AddHandler accessor.");
			}
			
			if(removeHandlerAccessorDeclaration == null)
			{
				Error("Need to provide RemoveHandler accessor.");
			}
			
			if(raiseEventAccessorDeclaration == null)
			{
				Error("Need to provide RaiseEvent accessor.");
			}
			
			EventDeclaration decl = new EventDeclaration(type, customEventName, m.Modifier, attributes);
			decl.StartLocation = m.GetDeclarationLocation(startPos);
			decl.EndLocation = t.EndLocation;
			decl.AddRegion = addHandlerAccessorDeclaration;
			decl.RemoveRegion = removeHandlerAccessorDeclaration;
			decl.RaiseRegion = raiseEventAccessorDeclaration;
			compilationUnit.AddChild(decl);
			
			break;
		}
		case 188: {
			lexer.NextToken();

#line  1309 "VBNET.ATG" 
			m.Check(Modifier.VBOperators);
			Point startPos = t.Location;
			TypeReference returnType = NullTypeReference.Instance;
			TypeReference operandType = NullTypeReference.Instance;
			string operandName;
			OverloadableOperatorType operatorType;
			AttributeSection section;
			List<ParameterDeclarationExpression> parameters = new List<ParameterDeclarationExpression>();
			List<AttributeSection> returnTypeAttributes = new List<AttributeSection>();
			
			OverloadableOperator(
#line  1319 "VBNET.ATG" 
out operatorType);
			Expect(24);
			if (la.kind == 55) {
				lexer.NextToken();
			}
			Identifier();

#line  1320 "VBNET.ATG" 
			operandName = t.val; 
			if (la.kind == 48) {
				lexer.NextToken();
				TypeName(
#line  1321 "VBNET.ATG" 
out operandType);
			}

#line  1322 "VBNET.ATG" 
			parameters.Add(new ParameterDeclarationExpression(operandType, operandName, ParamModifier.In)); 
			while (la.kind == 12) {
				lexer.NextToken();
				if (la.kind == 55) {
					lexer.NextToken();
				}
				Identifier();

#line  1326 "VBNET.ATG" 
				operandName = t.val; 
				if (la.kind == 48) {
					lexer.NextToken();
					TypeName(
#line  1327 "VBNET.ATG" 
out operandType);
				}

#line  1328 "VBNET.ATG" 
				parameters.Add(new ParameterDeclarationExpression(operandType, operandName, ParamModifier.In)); 
			}
			Expect(25);

#line  1331 "VBNET.ATG" 
			Point endPos = t.EndLocation; 
			if (la.kind == 48) {
				lexer.NextToken();
				while (la.kind == 27) {
					AttributeSection(
#line  1332 "VBNET.ATG" 
out section);

#line  1332 "VBNET.ATG" 
					returnTypeAttributes.Add(section); 
				}
				TypeName(
#line  1332 "VBNET.ATG" 
out returnType);

#line  1332 "VBNET.ATG" 
				endPos = t.EndLocation; 
				Expect(1);
			}
			Block(
#line  1333 "VBNET.ATG" 
out stmt);
			Expect(88);
			Expect(188);
			Expect(1);

#line  1335 "VBNET.ATG" 
			OperatorDeclaration operatorDeclaration = new OperatorDeclaration(m.Modifier, 
			                                                                 attributes, 
			                                                                 parameters, 
			                                                                 returnType,
			                                                                 operatorType
			                                                                 );
			operatorDeclaration.ConvertToType = returnType;
			operatorDeclaration.ReturnTypeAttributes = returnTypeAttributes;
			operatorDeclaration.Body = (BlockStatement)stmt;
			operatorDeclaration.StartLocation = m.GetDeclarationLocation(startPos);
			operatorDeclaration.EndLocation = endPos;
			operatorDeclaration.Body.StartLocation = startPos;
			operatorDeclaration.Body.EndLocation = t.Location;
			compilationUnit.AddChild(operatorDeclaration);
			
			break;
		}
		default: SynErr(227); break;
		}
	}

	void EnumMemberDecl(
#line  913 "VBNET.ATG" 
out FieldDeclaration f) {

#line  915 "VBNET.ATG" 
		Expression expr = null;List<AttributeSection> attributes = new List<AttributeSection>();
		AttributeSection section = null;
		VariableDeclaration varDecl = null;
		
		while (la.kind == 27) {
			AttributeSection(
#line  919 "VBNET.ATG" 
out section);

#line  919 "VBNET.ATG" 
			attributes.Add(section); 
		}
		Identifier();

#line  922 "VBNET.ATG" 
		f = new FieldDeclaration(attributes);
		varDecl = new VariableDeclaration(t.val);
		f.Fields.Add(varDecl);
		f.StartLocation = t.Location;
		
		if (la.kind == 11) {
			lexer.NextToken();
			Expr(
#line  927 "VBNET.ATG" 
out expr);

#line  927 "VBNET.ATG" 
			varDecl.Initializer = expr; 
		}
		Expect(1);
	}

	void InterfaceMemberDecl() {

#line  832 "VBNET.ATG" 
		TypeReference type =null;
		List<ParameterDeclarationExpression> p = new List<ParameterDeclarationExpression>();
		List<TemplateDefinition> templates = new List<TemplateDefinition>();
		AttributeSection section, returnTypeAttributeSection = null;
		Modifiers mod = new Modifiers();
		List<AttributeSection> attributes = new List<AttributeSection>();
		string name;
		
		if (StartOf(14)) {
			while (la.kind == 27) {
				AttributeSection(
#line  840 "VBNET.ATG" 
out section);

#line  840 "VBNET.ATG" 
				attributes.Add(section); 
			}
			while (StartOf(7)) {
				MemberModifier(
#line  844 "VBNET.ATG" 
mod);
			}
			if (la.kind == 93) {
				lexer.NextToken();

#line  847 "VBNET.ATG" 
				mod.Check(Modifier.VBInterfaceEvents); 
				Identifier();

#line  848 "VBNET.ATG" 
				name = t.val; 
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  849 "VBNET.ATG" 
p);
					}
					Expect(25);
				}
				if (la.kind == 48) {
					lexer.NextToken();
					TypeName(
#line  850 "VBNET.ATG" 
out type);
				}
				Expect(1);

#line  853 "VBNET.ATG" 
				EventDeclaration ed = new EventDeclaration(type, mod.Modifier, p, attributes, name, null);
				compilationUnit.AddChild(ed);
				ed.EndLocation = t.EndLocation;
				
			} else if (la.kind == 168) {
				lexer.NextToken();

#line  859 "VBNET.ATG" 
				mod.Check(Modifier.VBInterfaceMethods); 
				Identifier();

#line  860 "VBNET.ATG" 
				name = t.val; 
				TypeParameterList(
#line  861 "VBNET.ATG" 
templates);
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  862 "VBNET.ATG" 
p);
					}
					Expect(25);
				}
				Expect(1);

#line  865 "VBNET.ATG" 
				MethodDeclaration md = new MethodDeclaration(name, mod.Modifier, null, p, attributes);
				md.TypeReference = new TypeReference("", "System.Void");
				md.EndLocation = t.EndLocation;
				md.Templates = templates;
				compilationUnit.AddChild(md);
				
			} else if (la.kind == 100) {
				lexer.NextToken();

#line  873 "VBNET.ATG" 
				mod.Check(Modifier.VBInterfaceMethods); 
				Identifier();

#line  874 "VBNET.ATG" 
				name = t.val; 
				TypeParameterList(
#line  875 "VBNET.ATG" 
templates);
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  876 "VBNET.ATG" 
p);
					}
					Expect(25);
				}
				if (la.kind == 48) {
					lexer.NextToken();
					while (la.kind == 27) {
						AttributeSection(
#line  877 "VBNET.ATG" 
out returnTypeAttributeSection);
					}
					TypeName(
#line  877 "VBNET.ATG" 
out type);
				}

#line  879 "VBNET.ATG" 
				if(type == null) {
				type = new TypeReference("System.Object");
				}
				MethodDeclaration md = new MethodDeclaration(name, mod.Modifier, type, p, attributes);
				md.ReturnTypeAttributeSection = returnTypeAttributeSection;
				md.EndLocation = t.EndLocation;
				md.Templates = templates;
				compilationUnit.AddChild(md);
				
				Expect(1);
			} else if (la.kind == 147) {
				lexer.NextToken();

#line  891 "VBNET.ATG" 
				mod.Check(Modifier.VBInterfaceProperties); 
				Identifier();

#line  892 "VBNET.ATG" 
				name = t.val;  
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(4)) {
						FormalParameterList(
#line  893 "VBNET.ATG" 
p);
					}
					Expect(25);
				}
				if (la.kind == 48) {
					lexer.NextToken();
					TypeName(
#line  894 "VBNET.ATG" 
out type);
				}

#line  896 "VBNET.ATG" 
				if(type == null) {
				type = new TypeReference("System.Object");
				}
				
				Expect(1);

#line  902 "VBNET.ATG" 
				PropertyDeclaration pd = new PropertyDeclaration(name, type, mod.Modifier, attributes);
				pd.Parameters = p;
				pd.EndLocation = t.EndLocation;
				compilationUnit.AddChild(pd);
				
			} else SynErr(228);
		} else if (StartOf(15)) {
			NonModuleDeclaration(
#line  909 "VBNET.ATG" 
mod, attributes);
		} else SynErr(229);
	}

	void Expr(
#line  1695 "VBNET.ATG" 
out Expression expr) {
		ConditionalOrExpr(
#line  1697 "VBNET.ATG" 
out expr);
	}

	void ImplementsClause(
#line  1666 "VBNET.ATG" 
out List<InterfaceImplementation> baseInterfaces) {

#line  1668 "VBNET.ATG" 
		baseInterfaces = new List<InterfaceImplementation>();
		TypeReference type = null;
		string memberName = null;
		
		Expect(107);
		NonArrayTypeName(
#line  1673 "VBNET.ATG" 
out type, false);

#line  1674 "VBNET.ATG" 
		if (type != null) memberName = TypeReference.StripLastIdentifierFromType(ref type); 

#line  1675 "VBNET.ATG" 
		baseInterfaces.Add(new InterfaceImplementation(type, memberName)); 
		while (la.kind == 12) {
			lexer.NextToken();
			NonArrayTypeName(
#line  1677 "VBNET.ATG" 
out type, false);

#line  1678 "VBNET.ATG" 
			if (type != null) memberName = TypeReference.StripLastIdentifierFromType(ref type); 

#line  1679 "VBNET.ATG" 
			baseInterfaces.Add(new InterfaceImplementation(type, memberName)); 
		}
	}

	void HandlesClause(
#line  1624 "VBNET.ATG" 
out ArrayList handlesClause) {

#line  1626 "VBNET.ATG" 
		handlesClause = new ArrayList();
		string name;
		
		Expect(105);
		EventMemberSpecifier(
#line  1629 "VBNET.ATG" 
out name);

#line  1629 "VBNET.ATG" 
		handlesClause.Add(name); 
		while (la.kind == 12) {
			lexer.NextToken();
			EventMemberSpecifier(
#line  1630 "VBNET.ATG" 
out name);

#line  1630 "VBNET.ATG" 
			handlesClause.Add(name); 
		}
	}

	void Block(
#line  2287 "VBNET.ATG" 
out Statement stmt) {

#line  2290 "VBNET.ATG" 
		BlockStatement blockStmt = new BlockStatement();
		blockStmt.StartLocation = t.Location;
		compilationUnit.BlockStart(blockStmt);
		
		while (StartOf(16) || 
#line  2295 "VBNET.ATG" 
IsEndStmtAhead()) {
			if (
#line  2295 "VBNET.ATG" 
IsEndStmtAhead()) {
				Expect(88);
				EndOfStmt();

#line  2295 "VBNET.ATG" 
				compilationUnit.AddChild(new EndStatement()); 
			} else {
				Statement();
				EndOfStmt();
			}
		}

#line  2301 "VBNET.ATG" 
		stmt = blockStmt;
		blockStmt.EndLocation = t.EndLocation;
		compilationUnit.BlockEnd();
		
	}

	void Charset(
#line  1616 "VBNET.ATG" 
out CharsetModifier charsetModifier) {

#line  1617 "VBNET.ATG" 
		charsetModifier = CharsetModifier.None; 
		if (la.kind == 100 || la.kind == 168) {
		} else if (la.kind == 47) {
			lexer.NextToken();

#line  1618 "VBNET.ATG" 
			charsetModifier = CharsetModifier.ANSI; 
		} else if (la.kind == 50) {
			lexer.NextToken();

#line  1619 "VBNET.ATG" 
			charsetModifier = CharsetModifier.Auto; 
		} else if (la.kind == 177) {
			lexer.NextToken();

#line  1620 "VBNET.ATG" 
			charsetModifier = CharsetModifier.Unicode; 
		} else SynErr(230);
	}

	void VariableDeclarator(
#line  1522 "VBNET.ATG" 
List<VariableDeclaration> fieldDeclaration) {

#line  1524 "VBNET.ATG" 
		Expression expr = null;
		TypeReference type = null;ArrayList rank = null;ArrayList dimension = null;
		
		Identifier();

#line  1527 "VBNET.ATG" 
		string name = t.val; 
		if (
#line  1528 "VBNET.ATG" 
IsDims()) {
			ArrayNameModifier(
#line  1528 "VBNET.ATG" 
out rank);
		}
		if (
#line  1529 "VBNET.ATG" 
IsSize()) {
			ArrayInitializationModifier(
#line  1529 "VBNET.ATG" 
out dimension);
		}
		if (
#line  1531 "VBNET.ATG" 
IsObjectCreation()) {
			Expect(48);
			ObjectCreateExpression(
#line  1531 "VBNET.ATG" 
out expr);

#line  1533 "VBNET.ATG" 
			if (expr is ObjectCreateExpression) {
			type = ((ObjectCreateExpression)expr).CreateType;
			} else {
				type = ((ArrayCreateExpression)expr).CreateType;
			}
			
		} else if (StartOf(17)) {
			if (la.kind == 48) {
				lexer.NextToken();
				TypeName(
#line  1540 "VBNET.ATG" 
out type);
			}

#line  1542 "VBNET.ATG" 
			if (type != null && rank != null) {
			if(type.RankSpecifier != null) {
				Error("array rank only allowed one time");
			} else {
				type.RankSpecifier = (int[])rank.ToArray(typeof(int));
			}
			} else if (type != null && dimension != null) {
				if(type.RankSpecifier != null) {
					Error("array rank only allowed one time");
				} else {
					for (int i = 0; i < dimension.Count; i++)
						dimension[i] = Expression.AddInteger((Expression)dimension[i], 1);
					rank = new ArrayList();
					rank.Add(new ArrayCreationParameter(dimension));
					expr = new ArrayCreateExpression(type, rank);
					type = type.Clone();
					type.RankSpecifier = new int[] { dimension.Count - 1 };
				}
			}
			
			if (la.kind == 11) {
				lexer.NextToken();
				VariableInitializer(
#line  1562 "VBNET.ATG" 
out expr);
			}
		} else SynErr(231);

#line  1564 "VBNET.ATG" 
		fieldDeclaration.Add(new VariableDeclaration(name, expr, type)); 
	}

	void ConstantDeclarator(
#line  1505 "VBNET.ATG" 
List<VariableDeclaration> constantDeclaration) {

#line  1507 "VBNET.ATG" 
		Expression expr = null;
		TypeReference type = null;
		string name = String.Empty;
		
		Identifier();

#line  1511 "VBNET.ATG" 
		name = t.val; 
		if (la.kind == 48) {
			lexer.NextToken();
			TypeName(
#line  1512 "VBNET.ATG" 
out type);
		}
		Expect(11);
		Expr(
#line  1513 "VBNET.ATG" 
out expr);

#line  1515 "VBNET.ATG" 
		VariableDeclaration f = new VariableDeclaration(name, expr);
		f.TypeReference = type;
		constantDeclaration.Add(f);
		
	}

	void AccessorDecls(
#line  1447 "VBNET.ATG" 
out PropertyGetRegion getBlock, out PropertySetRegion setBlock) {

#line  1449 "VBNET.ATG" 
		List<AttributeSection> attributes = new List<AttributeSection>();
		AttributeSection section;
		getBlock = null;
		setBlock = null; 
		
		while (la.kind == 27) {
			AttributeSection(
#line  1454 "VBNET.ATG" 
out section);

#line  1454 "VBNET.ATG" 
			attributes.Add(section); 
		}
		if (la.kind == 101) {
			GetAccessorDecl(
#line  1456 "VBNET.ATG" 
out getBlock, attributes);
			if (la.kind == 27 || la.kind == 157) {

#line  1458 "VBNET.ATG" 
				attributes = new List<AttributeSection>(); 
				while (la.kind == 27) {
					AttributeSection(
#line  1459 "VBNET.ATG" 
out section);

#line  1459 "VBNET.ATG" 
					attributes.Add(section); 
				}
				SetAccessorDecl(
#line  1460 "VBNET.ATG" 
out setBlock, attributes);
			}
		} else if (la.kind == 157) {
			SetAccessorDecl(
#line  1463 "VBNET.ATG" 
out setBlock, attributes);
			if (la.kind == 27 || la.kind == 101) {

#line  1465 "VBNET.ATG" 
				attributes = new List<AttributeSection>(); 
				while (la.kind == 27) {
					AttributeSection(
#line  1466 "VBNET.ATG" 
out section);

#line  1466 "VBNET.ATG" 
					attributes.Add(section); 
				}
				GetAccessorDecl(
#line  1467 "VBNET.ATG" 
out getBlock, attributes);
			}
		} else SynErr(232);
	}

	void EventAccessorDeclaration(
#line  1410 "VBNET.ATG" 
out EventAddRemoveRegion eventAccessorDeclaration) {

#line  1412 "VBNET.ATG" 
		Statement stmt = null;
		List<ParameterDeclarationExpression> p = new List<ParameterDeclarationExpression>();
		AttributeSection section;
		List<AttributeSection> attributes = new List<AttributeSection>();
		eventAccessorDeclaration = null;
		
		while (la.kind == 27) {
			AttributeSection(
#line  1418 "VBNET.ATG" 
out section);

#line  1418 "VBNET.ATG" 
			attributes.Add(section); 
		}
		if (la.kind == 42) {
			lexer.NextToken();
			if (la.kind == 24) {
				lexer.NextToken();
				if (StartOf(4)) {
					FormalParameterList(
#line  1420 "VBNET.ATG" 
p);
				}
				Expect(25);
			}
			Expect(1);
			Block(
#line  1421 "VBNET.ATG" 
out stmt);
			Expect(88);
			Expect(42);
			Expect(1);

#line  1423 "VBNET.ATG" 
			eventAccessorDeclaration = new EventAddRegion(attributes);
			eventAccessorDeclaration.Block = (BlockStatement)stmt;
			eventAccessorDeclaration.Parameters = p;
			
		} else if (la.kind == 153) {
			lexer.NextToken();
			if (la.kind == 24) {
				lexer.NextToken();
				if (StartOf(4)) {
					FormalParameterList(
#line  1428 "VBNET.ATG" 
p);
				}
				Expect(25);
			}
			Expect(1);
			Block(
#line  1429 "VBNET.ATG" 
out stmt);
			Expect(88);
			Expect(153);
			Expect(1);

#line  1431 "VBNET.ATG" 
			eventAccessorDeclaration = new EventRemoveRegion(attributes);
			eventAccessorDeclaration.Block = (BlockStatement)stmt;
			eventAccessorDeclaration.Parameters = p;
			
		} else if (la.kind == 150) {
			lexer.NextToken();
			if (la.kind == 24) {
				lexer.NextToken();
				if (StartOf(4)) {
					FormalParameterList(
#line  1436 "VBNET.ATG" 
p);
				}
				Expect(25);
			}
			Expect(1);
			Block(
#line  1437 "VBNET.ATG" 
out stmt);
			Expect(88);
			Expect(150);
			Expect(1);

#line  1439 "VBNET.ATG" 
			eventAccessorDeclaration = new EventRaiseRegion(attributes);
			eventAccessorDeclaration.Block = (BlockStatement)stmt;
			eventAccessorDeclaration.Parameters = p;
			
		} else SynErr(233);
	}

	void OverloadableOperator(
#line  1352 "VBNET.ATG" 
out OverloadableOperatorType operatorType) {

#line  1353 "VBNET.ATG" 
		operatorType = OverloadableOperatorType.None; 
		switch (la.kind) {
		case 14: {
			lexer.NextToken();

#line  1355 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Add; 
			break;
		}
		case 15: {
			lexer.NextToken();

#line  1357 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Subtract; 
			break;
		}
		case 16: {
			lexer.NextToken();

#line  1359 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Multiply; 
			break;
		}
		case 17: {
			lexer.NextToken();

#line  1361 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Divide; 
			break;
		}
		case 18: {
			lexer.NextToken();

#line  1363 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.DivideInteger; 
			break;
		}
		case 19: {
			lexer.NextToken();

#line  1365 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Concat; 
			break;
		}
		case 116: {
			lexer.NextToken();

#line  1367 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Like; 
			break;
		}
		case 120: {
			lexer.NextToken();

#line  1369 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Modulus; 
			break;
		}
		case 45: {
			lexer.NextToken();

#line  1371 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.BitwiseAnd; 
			break;
		}
		case 138: {
			lexer.NextToken();

#line  1373 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.BitwiseOr; 
			break;
		}
		case 186: {
			lexer.NextToken();

#line  1375 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.ExclusiveOr; 
			break;
		}
		case 20: {
			lexer.NextToken();

#line  1377 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Power; 
			break;
		}
		case 31: {
			lexer.NextToken();

#line  1379 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.ShiftLeft; 
			break;
		}
		case 32: {
			lexer.NextToken();

#line  1381 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.ShiftRight; 
			break;
		}
		case 11: {
			lexer.NextToken();

#line  1383 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.Equality; 
			break;
		}
		case 28: {
			lexer.NextToken();

#line  1385 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.InEquality; 
			break;
		}
		case 27: {
			lexer.NextToken();

#line  1387 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.LessThan; 
			break;
		}
		case 30: {
			lexer.NextToken();

#line  1389 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.LessThanOrEqual; 
			break;
		}
		case 26: {
			lexer.NextToken();

#line  1391 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.GreaterThan; 
			break;
		}
		case 29: {
			lexer.NextToken();

#line  1393 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.GreaterThanOrEqual; 
			break;
		}
		case 75: {
			lexer.NextToken();

#line  1395 "VBNET.ATG" 
			operatorType = OverloadableOperatorType.CType; 
			break;
		}
		case 2: case 51: case 70: case 170: {
			Identifier();

#line  1399 "VBNET.ATG" 
			string opName = t.val; 
			if (string.Equals(opName, "istrue", StringComparison.InvariantCultureIgnoreCase)) {
				operatorType = OverloadableOperatorType.IsTrue;
			} else if (string.Equals(opName, "isfalse", StringComparison.InvariantCultureIgnoreCase)) {
				operatorType = OverloadableOperatorType.IsFalse;
			} else {
				Error("Invalid operator. Possible operators are '+', '-', 'Not', 'IsTrue', 'IsFalse'.");
			}
			
			break;
		}
		default: SynErr(234); break;
		}
	}

	void GetAccessorDecl(
#line  1473 "VBNET.ATG" 
out PropertyGetRegion getBlock, List<AttributeSection> attributes) {

#line  1474 "VBNET.ATG" 
		Statement stmt = null; 
		Expect(101);

#line  1476 "VBNET.ATG" 
		Point startLocation = t.Location; 
		Expect(1);
		Block(
#line  1478 "VBNET.ATG" 
out stmt);

#line  1479 "VBNET.ATG" 
		getBlock = new PropertyGetRegion((BlockStatement)stmt, attributes); 
		Expect(88);
		Expect(101);

#line  1481 "VBNET.ATG" 
		getBlock.StartLocation = startLocation; getBlock.EndLocation = t.EndLocation; 
		Expect(1);
	}

	void SetAccessorDecl(
#line  1486 "VBNET.ATG" 
out PropertySetRegion setBlock, List<AttributeSection> attributes) {

#line  1488 "VBNET.ATG" 
		Statement stmt = null; List<ParameterDeclarationExpression> p = new List<ParameterDeclarationExpression>();
		
		Expect(157);

#line  1491 "VBNET.ATG" 
		Point startLocation = t.Location; 
		if (la.kind == 24) {
			lexer.NextToken();
			if (StartOf(4)) {
				FormalParameterList(
#line  1492 "VBNET.ATG" 
p);
			}
			Expect(25);
		}
		Expect(1);
		Block(
#line  1494 "VBNET.ATG" 
out stmt);

#line  1496 "VBNET.ATG" 
		setBlock = new PropertySetRegion((BlockStatement)stmt, attributes);
		setBlock.Parameters = p;
		
		Expect(88);
		Expect(157);

#line  1500 "VBNET.ATG" 
		setBlock.StartLocation = startLocation; setBlock.EndLocation = t.EndLocation; 
		Expect(1);
	}

	void ArrayNameModifier(
#line  2080 "VBNET.ATG" 
out ArrayList arrayModifiers) {

#line  2082 "VBNET.ATG" 
		arrayModifiers = null;
		
		ArrayTypeModifiers(
#line  2084 "VBNET.ATG" 
out arrayModifiers);
	}

	void ArrayInitializationModifier(
#line  1568 "VBNET.ATG" 
out ArrayList arrayModifiers) {

#line  1570 "VBNET.ATG" 
		arrayModifiers = null;
		
		Expect(24);
		InitializationRankList(
#line  1572 "VBNET.ATG" 
out arrayModifiers);
		Expect(25);
	}

	void ObjectCreateExpression(
#line  1969 "VBNET.ATG" 
out Expression oce) {

#line  1971 "VBNET.ATG" 
		TypeReference type = null;
		Expression initializer = null;
		ArrayList arguments = null;
		oce = null;
		
		Expect(127);
		NonArrayTypeName(
#line  1976 "VBNET.ATG" 
out type, false);
		if (la.kind == 24) {
			lexer.NextToken();
			if (StartOf(18)) {
				ArgumentList(
#line  1977 "VBNET.ATG" 
out arguments);
			}
			Expect(25);
		}
		if (la.kind == 22) {
			ArrayInitializer(
#line  1978 "VBNET.ATG" 
out initializer);
		}

#line  1980 "VBNET.ATG" 
		if (initializer == null) {
		oce = new ObjectCreateExpression(type, arguments);
		} else {
			ArrayCreateExpression ace = new ArrayCreateExpression(type, initializer as ArrayInitializerExpression);
			ace.Parameters = arguments;
			oce = ace;
		}
		
	}

	void VariableInitializer(
#line  1588 "VBNET.ATG" 
out Expression initializerExpression) {

#line  1590 "VBNET.ATG" 
		initializerExpression = null;
		
		if (StartOf(19)) {
			Expr(
#line  1592 "VBNET.ATG" 
out initializerExpression);
		} else if (la.kind == 22) {
			ArrayInitializer(
#line  1593 "VBNET.ATG" 
out initializerExpression);
		} else SynErr(235);
	}

	void InitializationRankList(
#line  1576 "VBNET.ATG" 
out ArrayList rank) {

#line  1578 "VBNET.ATG" 
		rank = null;
		Expression expr = null;
		
		Expr(
#line  1581 "VBNET.ATG" 
out expr);

#line  1581 "VBNET.ATG" 
		rank = new ArrayList(); if (expr != null) { rank.Add(expr); } 
		while (la.kind == 12) {
			lexer.NextToken();
			Expr(
#line  1583 "VBNET.ATG" 
out expr);

#line  1583 "VBNET.ATG" 
			if (expr != null) { rank.Add(expr); } 
		}
	}

	void ArrayInitializer(
#line  1597 "VBNET.ATG" 
out Expression outExpr) {

#line  1599 "VBNET.ATG" 
		Expression expr = null;
		ArrayInitializerExpression initializer = new ArrayInitializerExpression();
		
		Expect(22);
		if (StartOf(20)) {
			VariableInitializer(
#line  1604 "VBNET.ATG" 
out expr);

#line  1606 "VBNET.ATG" 
			if (expr != null) { initializer.CreateExpressions.Add(expr); }
			
			while (
#line  1609 "VBNET.ATG" 
NotFinalComma()) {
				Expect(12);
				VariableInitializer(
#line  1609 "VBNET.ATG" 
out expr);

#line  1610 "VBNET.ATG" 
				if (expr != null) { initializer.CreateExpressions.Add(expr); } 
			}
		}
		Expect(23);

#line  1613 "VBNET.ATG" 
		outExpr = initializer; 
	}

	void EventMemberSpecifier(
#line  1683 "VBNET.ATG" 
out string name) {

#line  1684 "VBNET.ATG" 
		string type; name = String.Empty; 
		if (StartOf(10)) {
			Identifier();

#line  1685 "VBNET.ATG" 
			type = t.val; 
			Expect(10);
			Identifier();

#line  1687 "VBNET.ATG" 
			name = type + "." + t.val; 
		} else if (la.kind == 124) {
			lexer.NextToken();
			Expect(10);
			if (StartOf(10)) {
				Identifier();

#line  1690 "VBNET.ATG" 
				name = "MyBase." + t.val; 
			} else if (la.kind == 92) {
				lexer.NextToken();

#line  1691 "VBNET.ATG" 
				name = "MyBase.Error"; 
			} else SynErr(236);
		} else SynErr(237);
	}

	void NonArrayTypeName(
#line  2044 "VBNET.ATG" 
out TypeReference typeref, bool canBeUnbound) {

#line  2046 "VBNET.ATG" 
		string name;
		typeref = null;
		bool isGlobal = false;
		
		if (StartOf(21)) {
			if (la.kind == 199) {
				lexer.NextToken();
				Expect(10);

#line  2051 "VBNET.ATG" 
				isGlobal = true; 
			}
			QualIdentAndTypeArguments(
#line  2052 "VBNET.ATG" 
out typeref, canBeUnbound);

#line  2053 "VBNET.ATG" 
			typeref.IsGlobal = isGlobal; 
			while (la.kind == 10) {
				lexer.NextToken();

#line  2054 "VBNET.ATG" 
				TypeReference nestedTypeRef; 
				QualIdentAndTypeArguments(
#line  2055 "VBNET.ATG" 
out nestedTypeRef, canBeUnbound);

#line  2056 "VBNET.ATG" 
				typeref = new InnerClassTypeReference(typeref, nestedTypeRef.Type, nestedTypeRef.GenericTypes); 
			}
		} else if (la.kind == 133) {
			lexer.NextToken();

#line  2059 "VBNET.ATG" 
			typeref = new TypeReference("System.Object"); 
		} else if (StartOf(22)) {
			PrimitiveTypeName(
#line  2060 "VBNET.ATG" 
out name);

#line  2060 "VBNET.ATG" 
			typeref = new TypeReference(name); 
		} else SynErr(238);
	}

	void ConditionalOrExpr(
#line  1848 "VBNET.ATG" 
out Expression outExpr) {

#line  1849 "VBNET.ATG" 
		Expression expr; 
		ConditionalAndExpr(
#line  1850 "VBNET.ATG" 
out outExpr);
		while (la.kind == 139) {
			lexer.NextToken();
			ConditionalAndExpr(
#line  1850 "VBNET.ATG" 
out expr);

#line  1850 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.LogicalOr, expr);  
		}
	}

	void UnaryExpr(
#line  1704 "VBNET.ATG" 
out Expression uExpr) {

#line  1706 "VBNET.ATG" 
		Expression expr;
		UnaryOperatorType uop = UnaryOperatorType.None;
		bool isUOp = false;
		
		while (la.kind == 14 || la.kind == 15 || la.kind == 16) {
			if (la.kind == 14) {
				lexer.NextToken();

#line  1710 "VBNET.ATG" 
				uop = UnaryOperatorType.Plus; isUOp = true; 
			} else if (la.kind == 15) {
				lexer.NextToken();

#line  1711 "VBNET.ATG" 
				uop = UnaryOperatorType.Minus; isUOp = true; 
			} else {
				lexer.NextToken();

#line  1713 "VBNET.ATG" 
				uop = UnaryOperatorType.Star;  isUOp = true;
			}
		}
		SimpleExpr(
#line  1715 "VBNET.ATG" 
out expr);

#line  1717 "VBNET.ATG" 
		if (isUOp) {
		uExpr = new UnaryOperatorExpression(expr, uop);
		} else {
			uExpr = expr;
		}
		
	}

	void SimpleExpr(
#line  1740 "VBNET.ATG" 
out Expression pexpr) {

#line  1742 "VBNET.ATG" 
		Expression expr;
		TypeReference type = null;
		string name = String.Empty;
		pexpr = null;
		
		if (StartOf(23)) {
			switch (la.kind) {
			case 3: {
				lexer.NextToken();

#line  1750 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(t.literalValue, t.val);  
				break;
			}
			case 4: {
				lexer.NextToken();

#line  1751 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(t.literalValue, t.val);  
				break;
			}
			case 7: {
				lexer.NextToken();

#line  1752 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(t.literalValue, t.val);  
				break;
			}
			case 6: {
				lexer.NextToken();

#line  1753 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(t.literalValue, t.val);  
				break;
			}
			case 5: {
				lexer.NextToken();

#line  1754 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(t.literalValue, t.val);  
				break;
			}
			case 9: {
				lexer.NextToken();

#line  1755 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(t.literalValue, t.val);  
				break;
			}
			case 8: {
				lexer.NextToken();

#line  1756 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(t.literalValue, t.val);  
				break;
			}
			case 174: {
				lexer.NextToken();

#line  1758 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(true, "true");  
				break;
			}
			case 96: {
				lexer.NextToken();

#line  1759 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(false, "false"); 
				break;
			}
			case 130: {
				lexer.NextToken();

#line  1760 "VBNET.ATG" 
				pexpr = new PrimitiveExpression(null, "null");  
				break;
			}
			case 24: {
				lexer.NextToken();
				Expr(
#line  1761 "VBNET.ATG" 
out expr);
				Expect(25);

#line  1761 "VBNET.ATG" 
				pexpr = new ParenthesizedExpression(expr); 
				break;
			}
			case 2: case 51: case 70: case 170: {
				Identifier();

#line  1762 "VBNET.ATG" 
				pexpr = new IdentifierExpression(t.val); 
				break;
			}
			case 52: case 54: case 65: case 76: case 77: case 84: case 111: case 117: case 160: case 161: case 166: case 191: case 192: case 193: case 194: {

#line  1763 "VBNET.ATG" 
				string val = String.Empty; 
				PrimitiveTypeName(
#line  1764 "VBNET.ATG" 
out val);
				Expect(10);

#line  1765 "VBNET.ATG" 
				t.val = ""; 
				Identifier();

#line  1765 "VBNET.ATG" 
				pexpr = new FieldReferenceExpression(new TypeReferenceExpression(val), t.val); 
				break;
			}
			case 119: {
				lexer.NextToken();

#line  1766 "VBNET.ATG" 
				pexpr = new ThisReferenceExpression(); 
				break;
			}
			case 124: case 125: {

#line  1767 "VBNET.ATG" 
				Expression retExpr = null; 
				if (la.kind == 124) {
					lexer.NextToken();

#line  1768 "VBNET.ATG" 
					retExpr = new BaseReferenceExpression(); 
				} else if (la.kind == 125) {
					lexer.NextToken();

#line  1769 "VBNET.ATG" 
					retExpr = new ClassReferenceExpression(); 
				} else SynErr(239);
				Expect(10);
				IdentifierOrKeyword(
#line  1771 "VBNET.ATG" 
out name);

#line  1771 "VBNET.ATG" 
				pexpr = new FieldReferenceExpression(retExpr, name); 
				break;
			}
			case 199: {
				lexer.NextToken();
				Expect(10);
				Identifier();

#line  1773 "VBNET.ATG" 
				type = new TypeReference(t.val ?? ""); 

#line  1775 "VBNET.ATG" 
				type.IsGlobal = true; 

#line  1776 "VBNET.ATG" 
				pexpr = new TypeReferenceExpression(type); 
				break;
			}
			case 127: {
				ObjectCreateExpression(
#line  1777 "VBNET.ATG" 
out expr);

#line  1777 "VBNET.ATG" 
				pexpr = expr; 
				break;
			}
			case 75: case 82: {
				if (la.kind == 82) {
					lexer.NextToken();
				} else if (la.kind == 75) {
					lexer.NextToken();
				} else SynErr(240);
				Expect(24);
				Expr(
#line  1778 "VBNET.ATG" 
out expr);
				Expect(12);
				TypeName(
#line  1778 "VBNET.ATG" 
out type);
				Expect(25);

#line  1778 "VBNET.ATG" 
				pexpr = new CastExpression(type, expr); 
				break;
			}
			case 200: {
				lexer.NextToken();
				Expect(24);
				Expr(
#line  1779 "VBNET.ATG" 
out expr);
				Expect(12);
				TypeName(
#line  1779 "VBNET.ATG" 
out type);
				Expect(25);

#line  1779 "VBNET.ATG" 
				pexpr = new BinaryOperatorExpression(expr, BinaryOperatorType.AsCast, new TypeReferenceExpression(type)); 
				break;
			}
			case 59: case 60: case 61: case 62: case 63: case 64: case 66: case 68: case 69: case 72: case 73: case 74: case 195: case 196: case 197: case 198: {
				CastTarget(
#line  1780 "VBNET.ATG" 
out type);
				Expect(24);
				Expr(
#line  1780 "VBNET.ATG" 
out expr);
				Expect(25);

#line  1780 "VBNET.ATG" 
				pexpr = new CastExpression(type, expr, true); 
				break;
			}
			case 43: {
				lexer.NextToken();
				Expr(
#line  1781 "VBNET.ATG" 
out expr);

#line  1781 "VBNET.ATG" 
				pexpr = new AddressOfExpression(expr); 
				break;
			}
			case 102: {
				lexer.NextToken();
				Expect(24);
				GetTypeTypeName(
#line  1782 "VBNET.ATG" 
out type);
				Expect(25);

#line  1782 "VBNET.ATG" 
				pexpr = new TypeOfExpression(type); 
				break;
			}
			case 176: {
				lexer.NextToken();
				SimpleExpr(
#line  1783 "VBNET.ATG" 
out expr);
				Expect(113);
				TypeName(
#line  1783 "VBNET.ATG" 
out type);

#line  1783 "VBNET.ATG" 
				pexpr = new TypeOfIsExpression(expr, type); 
				break;
			}
			}
			while (la.kind == 10 || la.kind == 24) {
				InvocationOrMemberReferenceExpression(
#line  1785 "VBNET.ATG" 
ref pexpr);
			}
		} else if (la.kind == 10) {
			lexer.NextToken();
			IdentifierOrKeyword(
#line  1788 "VBNET.ATG" 
out name);

#line  1788 "VBNET.ATG" 
			pexpr = new FieldReferenceExpression(pexpr, name);
			while (la.kind == 10 || la.kind == 24) {
				InvocationOrMemberReferenceExpression(
#line  1789 "VBNET.ATG" 
ref pexpr);
			}
		} else SynErr(241);
	}

	void AssignmentOperator(
#line  1725 "VBNET.ATG" 
out AssignmentOperatorType op) {

#line  1726 "VBNET.ATG" 
		op = AssignmentOperatorType.None; 
		switch (la.kind) {
		case 11: {
			lexer.NextToken();

#line  1727 "VBNET.ATG" 
			op = AssignmentOperatorType.Assign; 
			break;
		}
		case 41: {
			lexer.NextToken();

#line  1728 "VBNET.ATG" 
			op = AssignmentOperatorType.ConcatString; 
			break;
		}
		case 33: {
			lexer.NextToken();

#line  1729 "VBNET.ATG" 
			op = AssignmentOperatorType.Add; 
			break;
		}
		case 35: {
			lexer.NextToken();

#line  1730 "VBNET.ATG" 
			op = AssignmentOperatorType.Subtract; 
			break;
		}
		case 36: {
			lexer.NextToken();

#line  1731 "VBNET.ATG" 
			op = AssignmentOperatorType.Multiply; 
			break;
		}
		case 37: {
			lexer.NextToken();

#line  1732 "VBNET.ATG" 
			op = AssignmentOperatorType.Divide; 
			break;
		}
		case 38: {
			lexer.NextToken();

#line  1733 "VBNET.ATG" 
			op = AssignmentOperatorType.DivideInteger; 
			break;
		}
		case 34: {
			lexer.NextToken();

#line  1734 "VBNET.ATG" 
			op = AssignmentOperatorType.Power; 
			break;
		}
		case 39: {
			lexer.NextToken();

#line  1735 "VBNET.ATG" 
			op = AssignmentOperatorType.ShiftLeft; 
			break;
		}
		case 40: {
			lexer.NextToken();

#line  1736 "VBNET.ATG" 
			op = AssignmentOperatorType.ShiftRight; 
			break;
		}
		default: SynErr(242); break;
		}
	}

	void IdentifierOrKeyword(
#line  2873 "VBNET.ATG" 
out string name) {

#line  2875 "VBNET.ATG" 
		lexer.NextToken(); name = t.val;  
	}

	void CastTarget(
#line  1826 "VBNET.ATG" 
out TypeReference type) {

#line  1828 "VBNET.ATG" 
		type = null;
		
		switch (la.kind) {
		case 59: {
			lexer.NextToken();

#line  1830 "VBNET.ATG" 
			type = new TypeReference("System.Boolean"); 
			break;
		}
		case 60: {
			lexer.NextToken();

#line  1831 "VBNET.ATG" 
			type = new TypeReference("System.Byte"); 
			break;
		}
		case 195: {
			lexer.NextToken();

#line  1832 "VBNET.ATG" 
			type = new TypeReference("System.SByte"); 
			break;
		}
		case 61: {
			lexer.NextToken();

#line  1833 "VBNET.ATG" 
			type = new TypeReference("System.Char"); 
			break;
		}
		case 62: {
			lexer.NextToken();

#line  1834 "VBNET.ATG" 
			type = new TypeReference("System.DateTime"); 
			break;
		}
		case 64: {
			lexer.NextToken();

#line  1835 "VBNET.ATG" 
			type = new TypeReference("System.Decimal"); 
			break;
		}
		case 63: {
			lexer.NextToken();

#line  1836 "VBNET.ATG" 
			type = new TypeReference("System.Double"); 
			break;
		}
		case 72: {
			lexer.NextToken();

#line  1837 "VBNET.ATG" 
			type = new TypeReference("System.Int16"); 
			break;
		}
		case 66: {
			lexer.NextToken();

#line  1838 "VBNET.ATG" 
			type = new TypeReference("System.Int32"); 
			break;
		}
		case 68: {
			lexer.NextToken();

#line  1839 "VBNET.ATG" 
			type = new TypeReference("System.Int64"); 
			break;
		}
		case 196: {
			lexer.NextToken();

#line  1840 "VBNET.ATG" 
			type = new TypeReference("System.UInt16"); 
			break;
		}
		case 197: {
			lexer.NextToken();

#line  1841 "VBNET.ATG" 
			type = new TypeReference("System.UInt32"); 
			break;
		}
		case 198: {
			lexer.NextToken();

#line  1842 "VBNET.ATG" 
			type = new TypeReference("System.UInt64"); 
			break;
		}
		case 69: {
			lexer.NextToken();

#line  1843 "VBNET.ATG" 
			type = new TypeReference("System.Object"); 
			break;
		}
		case 73: {
			lexer.NextToken();

#line  1844 "VBNET.ATG" 
			type = new TypeReference("System.Single"); 
			break;
		}
		case 74: {
			lexer.NextToken();

#line  1845 "VBNET.ATG" 
			type = new TypeReference("System.String"); 
			break;
		}
		default: SynErr(243); break;
		}
	}

	void GetTypeTypeName(
#line  2032 "VBNET.ATG" 
out TypeReference typeref) {

#line  2033 "VBNET.ATG" 
		ArrayList rank = null; 
		NonArrayTypeName(
#line  2035 "VBNET.ATG" 
out typeref, true);
		ArrayTypeModifiers(
#line  2036 "VBNET.ATG" 
out rank);

#line  2037 "VBNET.ATG" 
		if (rank != null && typeref != null) {
		typeref.RankSpecifier = (int[])rank.ToArray(typeof(int));
		}
		
	}

	void InvocationOrMemberReferenceExpression(
#line  1793 "VBNET.ATG" 
ref Expression pexpr) {

#line  1794 "VBNET.ATG" 
		string name; 
		if (la.kind == 10) {
			lexer.NextToken();
			IdentifierOrKeyword(
#line  1796 "VBNET.ATG" 
out name);

#line  1796 "VBNET.ATG" 
			pexpr = new FieldReferenceExpression(pexpr, name); 
		} else if (la.kind == 24) {
			InvocationExpression(
#line  1797 "VBNET.ATG" 
ref pexpr);
		} else SynErr(244);
	}

	void InvocationExpression(
#line  1800 "VBNET.ATG" 
ref Expression pexpr) {

#line  1801 "VBNET.ATG" 
		List<TypeReference> typeParameters = new List<TypeReference>();
		ArrayList parameters = null;
		TypeReference type; 
		Expect(24);

#line  1805 "VBNET.ATG" 
		Point start = t.Location; 
		if (la.kind == 201) {
			lexer.NextToken();
			TypeName(
#line  1807 "VBNET.ATG" 
out type);

#line  1807 "VBNET.ATG" 
			if (type != null) typeParameters.Add(type); 
			Expect(25);
			if (la.kind == 10) {
				lexer.NextToken();
				Identifier();

#line  1811 "VBNET.ATG" 
				pexpr = new FieldReferenceExpression(GetTypeReferenceExpression(pexpr, typeParameters), t.val); 
			} else if (la.kind == 24) {
				lexer.NextToken();
				ArgumentList(
#line  1813 "VBNET.ATG" 
out parameters);
				Expect(25);

#line  1815 "VBNET.ATG" 
				pexpr = new InvocationExpression(pexpr, parameters, typeParameters); 
			} else SynErr(245);
		} else if (StartOf(18)) {
			ArgumentList(
#line  1817 "VBNET.ATG" 
out parameters);
			Expect(25);

#line  1819 "VBNET.ATG" 
			pexpr = new InvocationExpression(pexpr, parameters, typeParameters); 
		} else SynErr(246);

#line  1821 "VBNET.ATG" 
		pexpr.StartLocation = start; pexpr.EndLocation = t.Location; 
	}

	void ArgumentList(
#line  1991 "VBNET.ATG" 
out ArrayList arguments) {

#line  1993 "VBNET.ATG" 
		arguments = new ArrayList();
		Expression expr = null;
		
		if (StartOf(19)) {
			Argument(
#line  1997 "VBNET.ATG" 
out expr);

#line  1997 "VBNET.ATG" 
			if (expr != null) { arguments.Add(expr); } 
			while (la.kind == 12) {
				lexer.NextToken();
				Argument(
#line  2000 "VBNET.ATG" 
out expr);

#line  2000 "VBNET.ATG" 
				if (expr != null) { arguments.Add(expr); } 
			}
		}
	}

	void ConditionalAndExpr(
#line  1853 "VBNET.ATG" 
out Expression outExpr) {

#line  1854 "VBNET.ATG" 
		Expression expr; 
		InclusiveOrExpr(
#line  1855 "VBNET.ATG" 
out outExpr);
		while (la.kind == 46) {
			lexer.NextToken();
			InclusiveOrExpr(
#line  1855 "VBNET.ATG" 
out expr);

#line  1855 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.LogicalAnd, expr);  
		}
	}

	void InclusiveOrExpr(
#line  1858 "VBNET.ATG" 
out Expression outExpr) {

#line  1859 "VBNET.ATG" 
		Expression expr; 
		ExclusiveOrExpr(
#line  1860 "VBNET.ATG" 
out outExpr);
		while (la.kind == 186) {
			lexer.NextToken();
			ExclusiveOrExpr(
#line  1860 "VBNET.ATG" 
out expr);

#line  1860 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.ExclusiveOr, expr);  
		}
	}

	void ExclusiveOrExpr(
#line  1863 "VBNET.ATG" 
out Expression outExpr) {

#line  1864 "VBNET.ATG" 
		Expression expr; 
		AndExpr(
#line  1865 "VBNET.ATG" 
out outExpr);
		while (la.kind == 138) {
			lexer.NextToken();
			AndExpr(
#line  1865 "VBNET.ATG" 
out expr);

#line  1865 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.BitwiseOr, expr);  
		}
	}

	void AndExpr(
#line  1868 "VBNET.ATG" 
out Expression outExpr) {

#line  1869 "VBNET.ATG" 
		Expression expr; 
		NotExpr(
#line  1870 "VBNET.ATG" 
out outExpr);
		while (la.kind == 45) {
			lexer.NextToken();
			NotExpr(
#line  1870 "VBNET.ATG" 
out expr);

#line  1870 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, BinaryOperatorType.BitwiseAnd, expr);  
		}
	}

	void NotExpr(
#line  1873 "VBNET.ATG" 
out Expression outExpr) {

#line  1874 "VBNET.ATG" 
		UnaryOperatorType uop = UnaryOperatorType.None; 
		while (la.kind == 129) {
			lexer.NextToken();

#line  1875 "VBNET.ATG" 
			uop = UnaryOperatorType.Not; 
		}
		EqualityExpr(
#line  1876 "VBNET.ATG" 
out outExpr);

#line  1877 "VBNET.ATG" 
		if (uop != UnaryOperatorType.None)
		outExpr = new UnaryOperatorExpression(outExpr, uop);
		
	}

	void EqualityExpr(
#line  1882 "VBNET.ATG" 
out Expression outExpr) {

#line  1884 "VBNET.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		RelationalExpr(
#line  1887 "VBNET.ATG" 
out outExpr);
		while (la.kind == 11 || la.kind == 28 || la.kind == 116) {
			if (la.kind == 28) {
				lexer.NextToken();

#line  1890 "VBNET.ATG" 
				op = BinaryOperatorType.InEquality; 
			} else if (la.kind == 11) {
				lexer.NextToken();

#line  1891 "VBNET.ATG" 
				op = BinaryOperatorType.Equality; 
			} else {
				lexer.NextToken();

#line  1892 "VBNET.ATG" 
				op = BinaryOperatorType.Like; 
			}
			RelationalExpr(
#line  1894 "VBNET.ATG" 
out expr);

#line  1894 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, op, expr);  
		}
	}

	void RelationalExpr(
#line  1898 "VBNET.ATG" 
out Expression outExpr) {

#line  1900 "VBNET.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		ShiftExpr(
#line  1903 "VBNET.ATG" 
out outExpr);
		while (StartOf(24)) {
			if (StartOf(25)) {
				if (la.kind == 27) {
					lexer.NextToken();

#line  1906 "VBNET.ATG" 
					op = BinaryOperatorType.LessThan; 
				} else if (la.kind == 26) {
					lexer.NextToken();

#line  1907 "VBNET.ATG" 
					op = BinaryOperatorType.GreaterThan; 
				} else if (la.kind == 30) {
					lexer.NextToken();

#line  1908 "VBNET.ATG" 
					op = BinaryOperatorType.LessThanOrEqual; 
				} else if (la.kind == 29) {
					lexer.NextToken();

#line  1909 "VBNET.ATG" 
					op = BinaryOperatorType.GreaterThanOrEqual; 
				} else SynErr(247);
				ShiftExpr(
#line  1911 "VBNET.ATG" 
out expr);

#line  1911 "VBNET.ATG" 
				outExpr = new BinaryOperatorExpression(outExpr, op, expr);  
			} else {
				if (la.kind == 113) {
					lexer.NextToken();

#line  1914 "VBNET.ATG" 
					op = BinaryOperatorType.ReferenceEquality; 
				} else if (la.kind == 190) {
					lexer.NextToken();

#line  1915 "VBNET.ATG" 
					op = BinaryOperatorType.ReferenceInequality; 
				} else SynErr(248);
				Expr(
#line  1916 "VBNET.ATG" 
out expr);

#line  1916 "VBNET.ATG" 
				outExpr = new BinaryOperatorExpression(outExpr, op, expr); 
			}
		}
	}

	void ShiftExpr(
#line  1920 "VBNET.ATG" 
out Expression outExpr) {

#line  1922 "VBNET.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		AdditiveExpr(
#line  1925 "VBNET.ATG" 
out outExpr);
		while (la.kind == 31 || la.kind == 32) {
			if (la.kind == 31) {
				lexer.NextToken();

#line  1928 "VBNET.ATG" 
				op = BinaryOperatorType.ShiftLeft; 
			} else {
				lexer.NextToken();

#line  1929 "VBNET.ATG" 
				op = BinaryOperatorType.ShiftRight; 
			}
			AdditiveExpr(
#line  1931 "VBNET.ATG" 
out expr);

#line  1931 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, op, expr);  
		}
	}

	void AdditiveExpr(
#line  1935 "VBNET.ATG" 
out Expression outExpr) {

#line  1937 "VBNET.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		MultiplicativeExpr(
#line  1940 "VBNET.ATG" 
out outExpr);
		while (la.kind == 14 || la.kind == 15 || la.kind == 19) {
			if (la.kind == 14) {
				lexer.NextToken();

#line  1943 "VBNET.ATG" 
				op = BinaryOperatorType.Add; 
			} else if (la.kind == 15) {
				lexer.NextToken();

#line  1944 "VBNET.ATG" 
				op = BinaryOperatorType.Subtract; 
			} else {
				lexer.NextToken();

#line  1945 "VBNET.ATG" 
				op = BinaryOperatorType.Concat; 
			}
			MultiplicativeExpr(
#line  1947 "VBNET.ATG" 
out expr);

#line  1947 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, op, expr);  
		}
	}

	void MultiplicativeExpr(
#line  1951 "VBNET.ATG" 
out Expression outExpr) {

#line  1953 "VBNET.ATG" 
		Expression expr;
		BinaryOperatorType op = BinaryOperatorType.None;
		
		UnaryExpr(
#line  1956 "VBNET.ATG" 
out outExpr);
		while (StartOf(26)) {
			if (la.kind == 16) {
				lexer.NextToken();

#line  1959 "VBNET.ATG" 
				op = BinaryOperatorType.Multiply; 
			} else if (la.kind == 17) {
				lexer.NextToken();

#line  1960 "VBNET.ATG" 
				op = BinaryOperatorType.Divide; 
			} else if (la.kind == 18) {
				lexer.NextToken();

#line  1961 "VBNET.ATG" 
				op = BinaryOperatorType.DivideInteger; 
			} else if (la.kind == 120) {
				lexer.NextToken();

#line  1962 "VBNET.ATG" 
				op = BinaryOperatorType.Modulus; 
			} else {
				lexer.NextToken();

#line  1963 "VBNET.ATG" 
				op = BinaryOperatorType.Power; 
			}
			UnaryExpr(
#line  1965 "VBNET.ATG" 
out expr);

#line  1965 "VBNET.ATG" 
			outExpr = new BinaryOperatorExpression(outExpr, op, expr); 
		}
	}

	void Argument(
#line  2006 "VBNET.ATG" 
out Expression argumentexpr) {

#line  2008 "VBNET.ATG" 
		Expression expr;
		argumentexpr = null;
		string name;
		
		if (
#line  2012 "VBNET.ATG" 
IsNamedAssign()) {
			Identifier();

#line  2012 "VBNET.ATG" 
			name = t.val;  
			Expect(13);
			Expect(11);
			Expr(
#line  2012 "VBNET.ATG" 
out expr);

#line  2014 "VBNET.ATG" 
			argumentexpr = new NamedArgumentExpression(name, expr);
			
		} else if (StartOf(19)) {
			Expr(
#line  2017 "VBNET.ATG" 
out argumentexpr);
		} else SynErr(249);
	}

	void ArrayTypeModifiers(
#line  2089 "VBNET.ATG" 
out ArrayList arrayModifiers) {

#line  2091 "VBNET.ATG" 
		arrayModifiers = new ArrayList();
		int i = 0;
		
		while (
#line  2094 "VBNET.ATG" 
IsDims()) {
			Expect(24);
			if (la.kind == 12 || la.kind == 25) {
				RankList(
#line  2096 "VBNET.ATG" 
out i);
			}

#line  2098 "VBNET.ATG" 
			arrayModifiers.Add(i);
			
			Expect(25);
		}

#line  2103 "VBNET.ATG" 
		if(arrayModifiers.Count == 0) {
		 arrayModifiers = null;
		}
		
	}

	void QualIdentAndTypeArguments(
#line  2063 "VBNET.ATG" 
out TypeReference typeref, bool canBeUnbound) {

#line  2064 "VBNET.ATG" 
		string name; typeref = null; 
		Qualident(
#line  2066 "VBNET.ATG" 
out name);

#line  2067 "VBNET.ATG" 
		typeref = new TypeReference(name); 
		if (
#line  2068 "VBNET.ATG" 
la.kind == Tokens.OpenParenthesis && Peek(1).kind == Tokens.Of) {
			lexer.NextToken();
			Expect(201);
			if (
#line  2070 "VBNET.ATG" 
canBeUnbound && (la.kind == Tokens.CloseParenthesis || la.kind == Tokens.Comma)) {

#line  2071 "VBNET.ATG" 
				typeref.GenericTypes.Add(NullTypeReference.Instance); 
				while (la.kind == 12) {
					lexer.NextToken();

#line  2072 "VBNET.ATG" 
					typeref.GenericTypes.Add(NullTypeReference.Instance); 
				}
			} else if (StartOf(5)) {
				TypeArgumentList(
#line  2073 "VBNET.ATG" 
typeref.GenericTypes);
			} else SynErr(250);
			Expect(25);
		}
	}

	void TypeArgumentList(
#line  2116 "VBNET.ATG" 
List<TypeReference> typeArguments) {

#line  2118 "VBNET.ATG" 
		TypeReference typeref;
		
		TypeName(
#line  2120 "VBNET.ATG" 
out typeref);

#line  2120 "VBNET.ATG" 
		if (typeref != null) typeArguments.Add(typeref); 
		while (la.kind == 12) {
			lexer.NextToken();
			TypeName(
#line  2123 "VBNET.ATG" 
out typeref);

#line  2123 "VBNET.ATG" 
			if (typeref != null) typeArguments.Add(typeref); 
		}
	}

	void RankList(
#line  2110 "VBNET.ATG" 
out int i) {

#line  2111 "VBNET.ATG" 
		i = 0; 
		while (la.kind == 12) {
			lexer.NextToken();

#line  2112 "VBNET.ATG" 
			++i; 
		}
	}

	void Attribute(
#line  2148 "VBNET.ATG" 
out ICSharpCode.NRefactory.Parser.AST.Attribute attribute) {

#line  2149 "VBNET.ATG" 
		string name;
		List<Expression> positional = new List<Expression>();
		List<NamedArgumentExpression> named = new List<NamedArgumentExpression>();
		
		if (la.kind == 199) {
			lexer.NextToken();
			Expect(10);
		}
		Qualident(
#line  2154 "VBNET.ATG" 
out name);
		if (la.kind == 24) {
			AttributeArguments(
#line  2155 "VBNET.ATG" 
positional, named);
		}

#line  2156 "VBNET.ATG" 
		attribute  = new ICSharpCode.NRefactory.Parser.AST.Attribute(name, positional, named); 
	}

	void AttributeArguments(
#line  2160 "VBNET.ATG" 
List<Expression> positional, List<NamedArgumentExpression> named) {

#line  2162 "VBNET.ATG" 
		bool nameFound = false;
		string name = "";
		Expression expr;
		
		Expect(24);
		if (
#line  2168 "VBNET.ATG" 
IsNotClosingParenthesis()) {
			if (
#line  2170 "VBNET.ATG" 
IsNamedAssign()) {

#line  2170 "VBNET.ATG" 
				nameFound = true; 
				IdentifierOrKeyword(
#line  2171 "VBNET.ATG" 
out name);
				if (la.kind == 13) {
					lexer.NextToken();
				}
				Expect(11);
			}
			Expr(
#line  2173 "VBNET.ATG" 
out expr);

#line  2175 "VBNET.ATG" 
			if (expr != null) { if(name == "") positional.Add(expr);
			else { named.Add(new NamedArgumentExpression(name, expr)); name = ""; }
			}
			
			while (la.kind == 12) {
				lexer.NextToken();
				if (
#line  2182 "VBNET.ATG" 
IsNamedAssign()) {

#line  2182 "VBNET.ATG" 
					nameFound = true; 
					IdentifierOrKeyword(
#line  2183 "VBNET.ATG" 
out name);
					if (la.kind == 13) {
						lexer.NextToken();
					}
					Expect(11);
				} else if (StartOf(19)) {

#line  2185 "VBNET.ATG" 
					if (nameFound) Error("no positional argument after named argument"); 
				} else SynErr(251);
				Expr(
#line  2186 "VBNET.ATG" 
out expr);

#line  2186 "VBNET.ATG" 
				if (expr != null) { if(name == "") positional.Add(expr);
				else { named.Add(new NamedArgumentExpression(name, expr)); name = ""; }
				}
				
			}
		}
		Expect(25);
	}

	void FormalParameter(
#line  2255 "VBNET.ATG" 
out ParameterDeclarationExpression p) {

#line  2257 "VBNET.ATG" 
		TypeReference type = null;
		ParamModifiers mod = new ParamModifiers(this);
		Expression expr = null;
		p = null;ArrayList arrayModifiers = null;
		
		while (StartOf(27)) {
			ParameterModifier(
#line  2262 "VBNET.ATG" 
mod);
		}
		Identifier();

#line  2263 "VBNET.ATG" 
		string parameterName = t.val; 
		if (
#line  2264 "VBNET.ATG" 
IsDims()) {
			ArrayTypeModifiers(
#line  2264 "VBNET.ATG" 
out arrayModifiers);
		}
		if (la.kind == 48) {
			lexer.NextToken();
			TypeName(
#line  2265 "VBNET.ATG" 
out type);
		}

#line  2267 "VBNET.ATG" 
		if(type != null) {
		if (arrayModifiers != null) {
			if (type.RankSpecifier != null) {
				Error("array rank only allowed one time");
			} else {
				type.RankSpecifier = (int[])arrayModifiers.ToArray(typeof(int));
			}
		}
		} else {
			type = new TypeReference("System.Object", arrayModifiers == null ? null : (int[])arrayModifiers.ToArray(typeof(int)));
		}
		
		if (la.kind == 11) {
			lexer.NextToken();
			Expr(
#line  2279 "VBNET.ATG" 
out expr);
		}

#line  2281 "VBNET.ATG" 
		mod.Check();
		p = new ParameterDeclarationExpression(type, parameterName, mod.Modifier, expr);
		
	}

	void ParameterModifier(
#line  2899 "VBNET.ATG" 
ParamModifiers m) {
		if (la.kind == 55) {
			lexer.NextToken();

#line  2900 "VBNET.ATG" 
			m.Add(ParamModifier.In); 
		} else if (la.kind == 53) {
			lexer.NextToken();

#line  2901 "VBNET.ATG" 
			m.Add(ParamModifier.Ref); 
		} else if (la.kind == 137) {
			lexer.NextToken();

#line  2902 "VBNET.ATG" 
			m.Add(ParamModifier.Optional); 
		} else if (la.kind == 144) {
			lexer.NextToken();

#line  2903 "VBNET.ATG" 
			m.Add(ParamModifier.Params); 
		} else SynErr(252);
	}

	void Statement() {

#line  2309 "VBNET.ATG" 
		Statement stmt = null;
		Point startPos = la.Location;
		string label = String.Empty;
		
		
		if (la.kind == 1 || la.kind == 13) {
		} else if (
#line  2315 "VBNET.ATG" 
IsLabel()) {
			LabelName(
#line  2315 "VBNET.ATG" 
out label);

#line  2317 "VBNET.ATG" 
			compilationUnit.AddChild(new LabelStatement(t.val));
			
			Expect(13);
			Statement();
		} else if (StartOf(28)) {
			EmbeddedStatement(
#line  2320 "VBNET.ATG" 
out stmt);

#line  2320 "VBNET.ATG" 
			compilationUnit.AddChild(stmt); 
		} else if (StartOf(29)) {
			LocalDeclarationStatement(
#line  2321 "VBNET.ATG" 
out stmt);

#line  2321 "VBNET.ATG" 
			compilationUnit.AddChild(stmt); 
		} else SynErr(253);

#line  2324 "VBNET.ATG" 
		if (stmt != null) {
		stmt.StartLocation = startPos;
		stmt.EndLocation = t.Location;
		}
		
	}

	void LabelName(
#line  2705 "VBNET.ATG" 
out string name) {

#line  2707 "VBNET.ATG" 
		name = String.Empty;
		
		if (StartOf(10)) {
			Identifier();

#line  2709 "VBNET.ATG" 
			name = t.val; 
		} else if (la.kind == 5) {
			lexer.NextToken();

#line  2710 "VBNET.ATG" 
			name = t.val; 
		} else SynErr(254);
	}

	void EmbeddedStatement(
#line  2363 "VBNET.ATG" 
out Statement statement) {

#line  2365 "VBNET.ATG" 
		Statement embeddedStatement = null;
		statement = null;
		Expression expr = null;
		string name = String.Empty;ArrayList p = null;
		
		switch (la.kind) {
		case 94: {
			lexer.NextToken();

#line  2370 "VBNET.ATG" 
			ExitType exitType = ExitType.None; 
			switch (la.kind) {
			case 168: {
				lexer.NextToken();

#line  2372 "VBNET.ATG" 
				exitType = ExitType.Sub; 
				break;
			}
			case 100: {
				lexer.NextToken();

#line  2374 "VBNET.ATG" 
				exitType = ExitType.Function; 
				break;
			}
			case 147: {
				lexer.NextToken();

#line  2376 "VBNET.ATG" 
				exitType = ExitType.Property; 
				break;
			}
			case 83: {
				lexer.NextToken();

#line  2378 "VBNET.ATG" 
				exitType = ExitType.Do; 
				break;
			}
			case 98: {
				lexer.NextToken();

#line  2380 "VBNET.ATG" 
				exitType = ExitType.For; 
				break;
			}
			case 175: {
				lexer.NextToken();

#line  2382 "VBNET.ATG" 
				exitType = ExitType.Try; 
				break;
			}
			case 182: {
				lexer.NextToken();

#line  2384 "VBNET.ATG" 
				exitType = ExitType.While; 
				break;
			}
			case 156: {
				lexer.NextToken();

#line  2386 "VBNET.ATG" 
				exitType = ExitType.Select; 
				break;
			}
			default: SynErr(255); break;
			}

#line  2388 "VBNET.ATG" 
			statement = new ExitStatement(exitType); 
			break;
		}
		case 175: {
			TryStatement(
#line  2389 "VBNET.ATG" 
out statement);
			break;
		}
		case 187: {
			lexer.NextToken();

#line  2390 "VBNET.ATG" 
			ContinueType continueType = ContinueType.None; 
			if (la.kind == 83 || la.kind == 98 || la.kind == 182) {
				if (la.kind == 83) {
					lexer.NextToken();

#line  2390 "VBNET.ATG" 
					continueType = ContinueType.Do; 
				} else if (la.kind == 98) {
					lexer.NextToken();

#line  2390 "VBNET.ATG" 
					continueType = ContinueType.For; 
				} else {
					lexer.NextToken();

#line  2390 "VBNET.ATG" 
					continueType = ContinueType.While; 
				}
			}

#line  2390 "VBNET.ATG" 
			statement = new ContinueStatement(continueType); 
			break;
		}
		case 172: {
			lexer.NextToken();
			if (StartOf(19)) {
				Expr(
#line  2392 "VBNET.ATG" 
out expr);
			}

#line  2392 "VBNET.ATG" 
			statement = new ThrowStatement(expr); 
			break;
		}
		case 155: {
			lexer.NextToken();
			if (StartOf(19)) {
				Expr(
#line  2394 "VBNET.ATG" 
out expr);
			}

#line  2394 "VBNET.ATG" 
			statement = new ReturnStatement(expr); 
			break;
		}
		case 169: {
			lexer.NextToken();
			Expr(
#line  2396 "VBNET.ATG" 
out expr);
			EndOfStmt();
			Block(
#line  2396 "VBNET.ATG" 
out embeddedStatement);
			Expect(88);
			Expect(169);

#line  2397 "VBNET.ATG" 
			statement = new LockStatement(expr, embeddedStatement); 
			break;
		}
		case 150: {
			lexer.NextToken();
			Identifier();

#line  2399 "VBNET.ATG" 
			name = t.val; 
			if (la.kind == 24) {
				lexer.NextToken();
				if (StartOf(18)) {
					ArgumentList(
#line  2400 "VBNET.ATG" 
out p);
				}
				Expect(25);
			}

#line  2401 "VBNET.ATG" 
			statement = new RaiseEventStatement(name, p); 
			break;
		}
		case 183: {
			WithStatement(
#line  2403 "VBNET.ATG" 
out statement);
			break;
		}
		case 42: {
			lexer.NextToken();

#line  2405 "VBNET.ATG" 
			Expression handlerExpr = null; 
			Expr(
#line  2406 "VBNET.ATG" 
out expr);
			Expect(12);
			Expr(
#line  2406 "VBNET.ATG" 
out handlerExpr);

#line  2408 "VBNET.ATG" 
			statement = new AddHandlerStatement(expr, handlerExpr);
			
			break;
		}
		case 153: {
			lexer.NextToken();

#line  2411 "VBNET.ATG" 
			Expression handlerExpr = null; 
			Expr(
#line  2412 "VBNET.ATG" 
out expr);
			Expect(12);
			Expr(
#line  2412 "VBNET.ATG" 
out handlerExpr);

#line  2414 "VBNET.ATG" 
			statement = new RemoveHandlerStatement(expr, handlerExpr);
			
			break;
		}
		case 182: {
			lexer.NextToken();
			Expr(
#line  2417 "VBNET.ATG" 
out expr);
			EndOfStmt();
			Block(
#line  2418 "VBNET.ATG" 
out embeddedStatement);
			Expect(88);
			Expect(182);

#line  2420 "VBNET.ATG" 
			statement = new DoLoopStatement(expr, embeddedStatement, ConditionType.While, ConditionPosition.Start);
			
			break;
		}
		case 83: {
			lexer.NextToken();

#line  2425 "VBNET.ATG" 
			ConditionType conditionType = ConditionType.None;
			
			if (la.kind == 178 || la.kind == 182) {
				WhileOrUntil(
#line  2428 "VBNET.ATG" 
out conditionType);
				Expr(
#line  2428 "VBNET.ATG" 
out expr);
				EndOfStmt();
				Block(
#line  2429 "VBNET.ATG" 
out embeddedStatement);
				Expect(118);

#line  2432 "VBNET.ATG" 
				statement = new DoLoopStatement(expr, 
				                               embeddedStatement, 
				                               conditionType == ConditionType.While ? ConditionType.DoWhile : conditionType, 
				                               ConditionPosition.Start);
				
			} else if (la.kind == 1 || la.kind == 13) {
				EndOfStmt();
				Block(
#line  2439 "VBNET.ATG" 
out embeddedStatement);
				Expect(118);
				if (la.kind == 178 || la.kind == 182) {
					WhileOrUntil(
#line  2440 "VBNET.ATG" 
out conditionType);
					Expr(
#line  2440 "VBNET.ATG" 
out expr);
				}

#line  2442 "VBNET.ATG" 
				statement = new DoLoopStatement(expr, embeddedStatement, conditionType, ConditionPosition.End);
				
			} else SynErr(256);
			break;
		}
		case 98: {
			lexer.NextToken();

#line  2447 "VBNET.ATG" 
			Expression group = null;
			TypeReference typeReference;
			string        typeName;
			Point startLocation = t.Location;
			
			if (la.kind == 85) {
				lexer.NextToken();
				LoopControlVariable(
#line  2454 "VBNET.ATG" 
out typeReference, out typeName);
				Expect(109);
				Expr(
#line  2455 "VBNET.ATG" 
out group);
				EndOfStmt();
				Block(
#line  2456 "VBNET.ATG" 
out embeddedStatement);
				Expect(128);
				if (StartOf(19)) {
					Expr(
#line  2457 "VBNET.ATG" 
out expr);
				}

#line  2459 "VBNET.ATG" 
				statement = new ForeachStatement(typeReference, 
				                                typeName,
				                                group, 
				                                embeddedStatement, 
				                                expr);
				statement.StartLocation = startLocation;
				statement.EndLocation   = t.EndLocation;
				
				
			} else if (StartOf(10)) {

#line  2470 "VBNET.ATG" 
				Expression start = null;
				Expression end = null;
				Expression step = null;
				Expression nextExpr = null;ArrayList nextExpressions = null;
				
				LoopControlVariable(
#line  2475 "VBNET.ATG" 
out typeReference, out typeName);
				Expect(11);
				Expr(
#line  2476 "VBNET.ATG" 
out start);
				Expect(173);
				Expr(
#line  2476 "VBNET.ATG" 
out end);
				if (la.kind == 163) {
					lexer.NextToken();
					Expr(
#line  2476 "VBNET.ATG" 
out step);
				}
				EndOfStmt();
				Block(
#line  2477 "VBNET.ATG" 
out embeddedStatement);
				Expect(128);
				if (StartOf(19)) {
					Expr(
#line  2480 "VBNET.ATG" 
out nextExpr);

#line  2480 "VBNET.ATG" 
					nextExpressions = new ArrayList(); nextExpressions.Add(nextExpr); 
					while (la.kind == 12) {
						lexer.NextToken();
						Expr(
#line  2481 "VBNET.ATG" 
out nextExpr);

#line  2481 "VBNET.ATG" 
						nextExpressions.Add(nextExpr); 
					}
				}

#line  2484 "VBNET.ATG" 
				statement = new ForNextStatement(typeReference, typeName, start, end, step, embeddedStatement, nextExpressions);
				
			} else SynErr(257);
			break;
		}
		case 92: {
			lexer.NextToken();
			Expr(
#line  2488 "VBNET.ATG" 
out expr);

#line  2488 "VBNET.ATG" 
			statement = new ErrorStatement(expr); 
			break;
		}
		case 152: {
			lexer.NextToken();

#line  2490 "VBNET.ATG" 
			Expression redimclause = null; bool isPreserve = false; 
			if (la.kind == 145) {
				lexer.NextToken();

#line  2490 "VBNET.ATG" 
				isPreserve = true; 
			}
			Expr(
#line  2491 "VBNET.ATG" 
out redimclause);

#line  2493 "VBNET.ATG" 
			ReDimStatement reDimStatement = new ReDimStatement(isPreserve);
			statement = reDimStatement;
			reDimStatement.ReDimClauses.Add(redimclause as InvocationExpression);
			
			while (la.kind == 12) {
				lexer.NextToken();
				Expr(
#line  2497 "VBNET.ATG" 
out redimclause);

#line  2497 "VBNET.ATG" 
				reDimStatement.ReDimClauses.Add(redimclause as InvocationExpression); 
			}
			break;
		}
		case 91: {
			lexer.NextToken();
			Expr(
#line  2500 "VBNET.ATG" 
out expr);

#line  2501 "VBNET.ATG" 
			ArrayList arrays = new ArrayList();
			if (expr != null) { arrays.Add(expr);}
			EraseStatement eraseStatement = new EraseStatement(arrays);
			
			
			while (la.kind == 12) {
				lexer.NextToken();
				Expr(
#line  2506 "VBNET.ATG" 
out expr);

#line  2506 "VBNET.ATG" 
				if (expr != null) { arrays.Add(expr); }
			}

#line  2507 "VBNET.ATG" 
			statement = eraseStatement; 
			break;
		}
		case 164: {
			lexer.NextToken();

#line  2509 "VBNET.ATG" 
			statement = new StopStatement(); 
			break;
		}
		case 106: {
			lexer.NextToken();
			Expr(
#line  2511 "VBNET.ATG" 
out expr);
			if (la.kind == 171) {
				lexer.NextToken();
			}
			if (
#line  2513 "VBNET.ATG" 
IsEndStmtAhead()) {
				Expect(88);

#line  2513 "VBNET.ATG" 
				statement = new IfElseStatement(expr, new EndStatement()); 
			} else if (la.kind == 1 || la.kind == 13) {
				EndOfStmt();
				Block(
#line  2516 "VBNET.ATG" 
out embeddedStatement);

#line  2518 "VBNET.ATG" 
				IfElseStatement ifStatement = new IfElseStatement(expr, embeddedStatement);
				
				while (la.kind == 87 || 
#line  2522 "VBNET.ATG" 
IsElseIf()) {
					if (
#line  2522 "VBNET.ATG" 
IsElseIf()) {
						Expect(86);
						Expect(106);
					} else {
						lexer.NextToken();
					}

#line  2525 "VBNET.ATG" 
					Expression condition = null; Statement block = null; 
					Expr(
#line  2526 "VBNET.ATG" 
out condition);
					if (la.kind == 171) {
						lexer.NextToken();
					}
					EndOfStmt();
					Block(
#line  2527 "VBNET.ATG" 
out block);

#line  2529 "VBNET.ATG" 
					ifStatement.ElseIfSections.Add(new ElseIfSection(condition, block));
					
				}
				if (la.kind == 86) {
					lexer.NextToken();
					EndOfStmt();
					Block(
#line  2534 "VBNET.ATG" 
out embeddedStatement);

#line  2536 "VBNET.ATG" 
					ifStatement.FalseStatement.Add(embeddedStatement);
					
				}
				Expect(88);
				Expect(106);

#line  2540 "VBNET.ATG" 
				statement = ifStatement;
				
			} else if (StartOf(28)) {
				EmbeddedStatement(
#line  2543 "VBNET.ATG" 
out embeddedStatement);

#line  2545 "VBNET.ATG" 
				IfElseStatement ifStatement = new IfElseStatement(expr, embeddedStatement);
				
				while (la.kind == 13) {
					lexer.NextToken();
					EmbeddedStatement(
#line  2547 "VBNET.ATG" 
out embeddedStatement);

#line  2547 "VBNET.ATG" 
					ifStatement.TrueStatement.Add(embeddedStatement); 
				}
				if (la.kind == 86) {
					lexer.NextToken();
					if (StartOf(28)) {
						EmbeddedStatement(
#line  2549 "VBNET.ATG" 
out embeddedStatement);
					}

#line  2551 "VBNET.ATG" 
					ifStatement.FalseStatement.Add(embeddedStatement);
					
					while (la.kind == 13) {
						lexer.NextToken();
						EmbeddedStatement(
#line  2554 "VBNET.ATG" 
out embeddedStatement);

#line  2555 "VBNET.ATG" 
						ifStatement.FalseStatement.Add(embeddedStatement); 
					}
				}

#line  2558 "VBNET.ATG" 
				statement = ifStatement; 
			} else SynErr(258);
			break;
		}
		case 156: {
			lexer.NextToken();
			if (la.kind == 57) {
				lexer.NextToken();
			}
			Expr(
#line  2561 "VBNET.ATG" 
out expr);
			EndOfStmt();

#line  2562 "VBNET.ATG" 
			List<SwitchSection> selectSections = new List<SwitchSection>();
			Statement block = null;
			
			while (la.kind == 57) {

#line  2566 "VBNET.ATG" 
				List<CaseLabel> caseClauses = null; 
				lexer.NextToken();
				CaseClauses(
#line  2567 "VBNET.ATG" 
out caseClauses);
				if (
#line  2567 "VBNET.ATG" 
IsNotStatementSeparator()) {
					lexer.NextToken();
				}
				EndOfStmt();

#line  2569 "VBNET.ATG" 
				SwitchSection selectSection = new SwitchSection(caseClauses);
				
				Block(
#line  2571 "VBNET.ATG" 
out block);

#line  2573 "VBNET.ATG" 
				selectSection.Children = block.Children;
				selectSections.Add(selectSection);
				
			}

#line  2577 "VBNET.ATG" 
			statement = new SwitchStatement(expr, selectSections); 
			Expect(88);
			Expect(156);
			break;
		}
		case 135: {

#line  2579 "VBNET.ATG" 
			OnErrorStatement onErrorStatement = null; 
			OnErrorStatement(
#line  2580 "VBNET.ATG" 
out onErrorStatement);

#line  2580 "VBNET.ATG" 
			statement = onErrorStatement; 
			break;
		}
		case 104: {

#line  2581 "VBNET.ATG" 
			GotoStatement goToStatement = null; 
			GotoStatement(
#line  2582 "VBNET.ATG" 
out goToStatement);

#line  2582 "VBNET.ATG" 
			statement = goToStatement; 
			break;
		}
		case 154: {

#line  2583 "VBNET.ATG" 
			ResumeStatement resumeStatement = null; 
			ResumeStatement(
#line  2584 "VBNET.ATG" 
out resumeStatement);

#line  2584 "VBNET.ATG" 
			statement = resumeStatement; 
			break;
		}
		case 2: case 3: case 4: case 5: case 6: case 7: case 8: case 9: case 10: case 14: case 15: case 16: case 24: case 43: case 51: case 52: case 54: case 59: case 60: case 61: case 62: case 63: case 64: case 65: case 66: case 68: case 69: case 70: case 72: case 73: case 74: case 75: case 76: case 77: case 82: case 84: case 96: case 102: case 111: case 117: case 119: case 124: case 125: case 127: case 130: case 160: case 161: case 166: case 170: case 174: case 176: case 191: case 192: case 193: case 194: case 195: case 196: case 197: case 198: case 199: case 200: {

#line  2587 "VBNET.ATG" 
			Expression val = null;
			AssignmentOperatorType op;
			
			bool mustBeAssignment = la.kind == Tokens.Plus  || la.kind == Tokens.Minus ||
			                        la.kind == Tokens.Not   || la.kind == Tokens.Times;
			
			UnaryExpr(
#line  2593 "VBNET.ATG" 
out expr);
			if (StartOf(30)) {
				AssignmentOperator(
#line  2595 "VBNET.ATG" 
out op);
				Expr(
#line  2595 "VBNET.ATG" 
out val);

#line  2595 "VBNET.ATG" 
				expr = new AssignmentExpression(expr, op, val); 
			} else if (la.kind == 1 || la.kind == 13 || la.kind == 86) {

#line  2596 "VBNET.ATG" 
				if (mustBeAssignment) Error("error in assignment."); 
			} else SynErr(259);

#line  2599 "VBNET.ATG" 
			// a field reference expression that stands alone is a
			// invocation expression without parantheses and arguments
			if(expr is FieldReferenceExpression || expr is IdentifierExpression) {
				expr = new InvocationExpression(expr);
			}
			statement = new StatementExpression(expr);
			
			break;
		}
		case 56: {
			lexer.NextToken();
			UnaryExpr(
#line  2606 "VBNET.ATG" 
out expr);

#line  2606 "VBNET.ATG" 
			statement = new StatementExpression(expr); 
			break;
		}
		case 189: {
			lexer.NextToken();
			Identifier();

#line  2608 "VBNET.ATG" 
			string resourcename = t.val, typeName; 
			Statement resourceAquisition = null, block = null;
			
			Expect(48);
			if (la.kind == 127) {
				lexer.NextToken();
				Qualident(
#line  2612 "VBNET.ATG" 
out typeName);

#line  2612 "VBNET.ATG" 
				ArrayList initializer = null; 
				if (la.kind == 24) {
					lexer.NextToken();
					if (StartOf(18)) {
						ArgumentList(
#line  2612 "VBNET.ATG" 
out initializer);
					}
					Expect(25);
				}

#line  2614 "VBNET.ATG" 
				resourceAquisition =  new LocalVariableDeclaration(new VariableDeclaration(resourcename, new ArrayInitializerExpression(initializer), new TypeReference(typeName)));
				
				
			} else if (StartOf(10)) {
				Qualident(
#line  2617 "VBNET.ATG" 
out typeName);
				Expect(11);
				Expr(
#line  2617 "VBNET.ATG" 
out expr);

#line  2619 "VBNET.ATG" 
				resourceAquisition =  new LocalVariableDeclaration(new VariableDeclaration(resourcename, expr, new TypeReference(typeName)));
				
			} else SynErr(260);
			Block(
#line  2622 "VBNET.ATG" 
out block);
			Expect(88);
			Expect(189);

#line  2624 "VBNET.ATG" 
			statement = new UsingStatement(resourceAquisition, block); 
			break;
		}
		default: SynErr(261); break;
		}
	}

	void LocalDeclarationStatement(
#line  2332 "VBNET.ATG" 
out Statement statement) {

#line  2334 "VBNET.ATG" 
		Modifiers m = new Modifiers();
		LocalVariableDeclaration localVariableDeclaration;
		bool dimfound = false;
		
		while (la.kind == 71 || la.kind == 81 || la.kind == 162) {
			if (la.kind == 71) {
				lexer.NextToken();

#line  2340 "VBNET.ATG" 
				m.Add(Modifier.Const, t.Location); 
			} else if (la.kind == 162) {
				lexer.NextToken();

#line  2341 "VBNET.ATG" 
				m.Add(Modifier.Static, t.Location); 
			} else {
				lexer.NextToken();

#line  2342 "VBNET.ATG" 
				dimfound = true; 
			}
		}

#line  2345 "VBNET.ATG" 
		if(dimfound && (m.Modifier & Modifier.Const) != 0) {
		Error("Dim is not allowed on constants.");
		}
		
		if(m.isNone && dimfound == false) {
			Error("Const, Dim or Static expected");
		}
		
		localVariableDeclaration = new LocalVariableDeclaration(m.Modifier);
		localVariableDeclaration.StartLocation = t.Location;
		
		VariableDeclarator(
#line  2356 "VBNET.ATG" 
localVariableDeclaration.Variables);
		while (la.kind == 12) {
			lexer.NextToken();
			VariableDeclarator(
#line  2357 "VBNET.ATG" 
localVariableDeclaration.Variables);
		}

#line  2359 "VBNET.ATG" 
		statement = localVariableDeclaration;
		
	}

	void TryStatement(
#line  2817 "VBNET.ATG" 
out Statement tryStatement) {

#line  2819 "VBNET.ATG" 
		Statement blockStmt = null, finallyStmt = null;List<CatchClause> catchClauses = null;
		
		Expect(175);
		EndOfStmt();
		Block(
#line  2822 "VBNET.ATG" 
out blockStmt);
		if (la.kind == 58 || la.kind == 88 || la.kind == 97) {
			CatchClauses(
#line  2823 "VBNET.ATG" 
out catchClauses);
		}
		if (la.kind == 97) {
			lexer.NextToken();
			EndOfStmt();
			Block(
#line  2824 "VBNET.ATG" 
out finallyStmt);
		}
		Expect(88);
		Expect(175);

#line  2827 "VBNET.ATG" 
		tryStatement = new TryCatchStatement(blockStmt, catchClauses, finallyStmt);
		
	}

	void WithStatement(
#line  2795 "VBNET.ATG" 
out Statement withStatement) {

#line  2797 "VBNET.ATG" 
		Statement blockStmt = null;
		Expression expr = null;
		
		Expect(183);

#line  2800 "VBNET.ATG" 
		Point start = t.Location; 
		Expr(
#line  2801 "VBNET.ATG" 
out expr);
		EndOfStmt();

#line  2803 "VBNET.ATG" 
		withStatement = new WithStatement(expr);
		withStatement.StartLocation = start;
		withStatements.Push(withStatement);
		
		Block(
#line  2807 "VBNET.ATG" 
out blockStmt);

#line  2809 "VBNET.ATG" 
		((WithStatement)withStatement).Body = (BlockStatement)blockStmt;
		withStatements.Pop();
		
		Expect(88);
		Expect(183);

#line  2813 "VBNET.ATG" 
		withStatement.EndLocation = t.Location; 
	}

	void WhileOrUntil(
#line  2788 "VBNET.ATG" 
out ConditionType conditionType) {

#line  2789 "VBNET.ATG" 
		conditionType = ConditionType.None; 
		if (la.kind == 182) {
			lexer.NextToken();

#line  2790 "VBNET.ATG" 
			conditionType = ConditionType.While; 
		} else if (la.kind == 178) {
			lexer.NextToken();

#line  2791 "VBNET.ATG" 
			conditionType = ConditionType.Until; 
		} else SynErr(262);
	}

	void LoopControlVariable(
#line  2629 "VBNET.ATG" 
out TypeReference type, out string name) {

#line  2630 "VBNET.ATG" 
		ArrayList arrayModifiers = null;
		type = null;
		
		Qualident(
#line  2634 "VBNET.ATG" 
out name);
		if (
#line  2635 "VBNET.ATG" 
IsDims()) {
			ArrayTypeModifiers(
#line  2635 "VBNET.ATG" 
out arrayModifiers);
		}
		if (la.kind == 48) {
			lexer.NextToken();
			TypeName(
#line  2636 "VBNET.ATG" 
out type);

#line  2636 "VBNET.ATG" 
			if (name.IndexOf('.') > 0) { Error("No type def for 'for each' member indexer allowed."); } 
		}

#line  2638 "VBNET.ATG" 
		if (type != null) {
		if(type.RankSpecifier != null && arrayModifiers != null) {
			Error("array rank only allowed one time");
		} else if (arrayModifiers != null) {
			type.RankSpecifier = (int[])arrayModifiers.ToArray(typeof(int));
		}
		} else {
			if (arrayModifiers != null) {
				type = new TypeReference("Integer", (int[])arrayModifiers.ToArray(typeof(int)));
			} else {
				type = new TypeReference("Integer");
			}
		}
		
	}

	void CaseClauses(
#line  2748 "VBNET.ATG" 
out List<CaseLabel> caseClauses) {

#line  2750 "VBNET.ATG" 
		caseClauses = new List<CaseLabel>();
		CaseLabel caseClause = null;
		
		CaseClause(
#line  2753 "VBNET.ATG" 
out caseClause);

#line  2753 "VBNET.ATG" 
		caseClauses.Add(caseClause); 
		while (la.kind == 12) {
			lexer.NextToken();
			CaseClause(
#line  2754 "VBNET.ATG" 
out caseClause);

#line  2754 "VBNET.ATG" 
			caseClauses.Add(caseClause); 
		}
	}

	void OnErrorStatement(
#line  2655 "VBNET.ATG" 
out OnErrorStatement stmt) {

#line  2657 "VBNET.ATG" 
		stmt = null;
		GotoStatement goToStatement = null;
		
		Expect(135);
		Expect(92);
		if (
#line  2663 "VBNET.ATG" 
IsNegativeLabelName()) {
			Expect(104);
			Expect(15);
			Expect(5);

#line  2665 "VBNET.ATG" 
			long intLabel = Int64.Parse(t.val);
			if(intLabel != 1) {
				Error("invalid label in on error statement.");
			}
			stmt = new OnErrorStatement(new GotoStatement((intLabel * -1).ToString()));
			
		} else if (la.kind == 104) {
			GotoStatement(
#line  2671 "VBNET.ATG" 
out goToStatement);

#line  2673 "VBNET.ATG" 
			string val = goToStatement.Label;
			
			// if value is numeric, make sure that is 0
			try {
				long intLabel = Int64.Parse(val);
				if(intLabel != 0) {
					Error("invalid label in on error statement.");
				}
			} catch {
			}
			stmt = new OnErrorStatement(goToStatement);
			
		} else if (la.kind == 154) {
			lexer.NextToken();
			Expect(128);

#line  2687 "VBNET.ATG" 
			stmt = new OnErrorStatement(new ResumeStatement(true));
			
		} else SynErr(263);
	}

	void GotoStatement(
#line  2693 "VBNET.ATG" 
out ICSharpCode.NRefactory.Parser.AST.GotoStatement goToStatement) {

#line  2695 "VBNET.ATG" 
		string label = String.Empty;
		
		Expect(104);
		LabelName(
#line  2698 "VBNET.ATG" 
out label);

#line  2700 "VBNET.ATG" 
		goToStatement = new ICSharpCode.NRefactory.Parser.AST.GotoStatement(label);
		
	}

	void ResumeStatement(
#line  2737 "VBNET.ATG" 
out ResumeStatement resumeStatement) {

#line  2739 "VBNET.ATG" 
		resumeStatement = null;
		string label = String.Empty;
		
		if (
#line  2742 "VBNET.ATG" 
IsResumeNext()) {
			Expect(154);
			Expect(128);

#line  2743 "VBNET.ATG" 
			resumeStatement = new ResumeStatement(true); 
		} else if (la.kind == 154) {
			lexer.NextToken();
			if (StartOf(31)) {
				LabelName(
#line  2744 "VBNET.ATG" 
out label);
			}

#line  2744 "VBNET.ATG" 
			resumeStatement = new ResumeStatement(label); 
		} else SynErr(264);
	}

	void CaseClause(
#line  2758 "VBNET.ATG" 
out CaseLabel caseClause) {

#line  2760 "VBNET.ATG" 
		Expression expr = null;
		Expression sexpr = null;
		BinaryOperatorType op = BinaryOperatorType.None;
		caseClause = null;
		
		if (la.kind == 86) {
			lexer.NextToken();

#line  2766 "VBNET.ATG" 
			caseClause = new CaseLabel(); 
		} else if (StartOf(32)) {
			if (la.kind == 113) {
				lexer.NextToken();
			}
			switch (la.kind) {
			case 27: {
				lexer.NextToken();

#line  2770 "VBNET.ATG" 
				op = BinaryOperatorType.LessThan; 
				break;
			}
			case 26: {
				lexer.NextToken();

#line  2771 "VBNET.ATG" 
				op = BinaryOperatorType.GreaterThan; 
				break;
			}
			case 30: {
				lexer.NextToken();

#line  2772 "VBNET.ATG" 
				op = BinaryOperatorType.LessThanOrEqual; 
				break;
			}
			case 29: {
				lexer.NextToken();

#line  2773 "VBNET.ATG" 
				op = BinaryOperatorType.GreaterThanOrEqual; 
				break;
			}
			case 11: {
				lexer.NextToken();

#line  2774 "VBNET.ATG" 
				op = BinaryOperatorType.Equality; 
				break;
			}
			case 28: {
				lexer.NextToken();

#line  2775 "VBNET.ATG" 
				op = BinaryOperatorType.InEquality; 
				break;
			}
			default: SynErr(265); break;
			}
			Expr(
#line  2777 "VBNET.ATG" 
out expr);

#line  2779 "VBNET.ATG" 
			caseClause = new CaseLabel(op, expr);
			
		} else if (StartOf(19)) {
			Expr(
#line  2781 "VBNET.ATG" 
out expr);
			if (la.kind == 173) {
				lexer.NextToken();
				Expr(
#line  2781 "VBNET.ATG" 
out sexpr);
			}

#line  2783 "VBNET.ATG" 
			caseClause = new CaseLabel(expr, sexpr);
			
		} else SynErr(266);
	}

	void CatchClauses(
#line  2832 "VBNET.ATG" 
out List<CatchClause> catchClauses) {

#line  2834 "VBNET.ATG" 
		catchClauses = new List<CatchClause>();
		TypeReference type = null;
		Statement blockStmt = null;
		Expression expr = null;
		string name = String.Empty;
		
		while (la.kind == 58) {
			lexer.NextToken();
			if (StartOf(10)) {
				Identifier();

#line  2842 "VBNET.ATG" 
				name = t.val; 
				if (la.kind == 48) {
					lexer.NextToken();
					TypeName(
#line  2842 "VBNET.ATG" 
out type);
				}
			}
			if (la.kind == 181) {
				lexer.NextToken();
				Expr(
#line  2843 "VBNET.ATG" 
out expr);
			}
			EndOfStmt();
			Block(
#line  2845 "VBNET.ATG" 
out blockStmt);

#line  2846 "VBNET.ATG" 
			catchClauses.Add(new CatchClause(type, name, blockStmt, expr)); 
		}
	}


	public Parser(ILexer lexer) : base(lexer)
	{
	}
	
	public override void Parse()
	{
		VBNET();

	}
	
	protected void ExpectWeak(int n, int follow)
	{
		if (lexer.LookAhead.kind == n) {
			lexer.NextToken();
		} else {
			SynErr(n);
			while (!StartOf(follow)) {
				lexer.NextToken();
			}
		}
	}
	
	protected bool WeakSeparator(int n, int syFol, int repFol)
	{
		bool[] s = new bool[maxT + 1];
		
		if (lexer.LookAhead.kind == n) {
			lexer.NextToken();
			return true;
		} else if (StartOf(repFol)) {
			return false;
		} else {
			for (int i = 0; i <= maxT; i++) {
				s[i] = set[syFol, i] || set[repFol, i] || set[0, i];
			}
			SynErr(n);
			while (!s[lexer.LookAhead.kind]) {
				lexer.NextToken();
			}
			return StartOf(syFol);
		}
	}
	
	protected override void SynErr(int line, int col, int errorNumber)
	{
		errors.count++; 
		string s;
		switch (errorNumber) {
			case 0: s = "EOF expected"; break;
			case 1: s = "EOL expected"; break;
			case 2: s = "ident expected"; break;
			case 3: s = "LiteralString expected"; break;
			case 4: s = "LiteralCharacter expected"; break;
			case 5: s = "LiteralInteger expected"; break;
			case 6: s = "LiteralDouble expected"; break;
			case 7: s = "LiteralSingle expected"; break;
			case 8: s = "LiteralDecimal expected"; break;
			case 9: s = "LiteralDate expected"; break;
			case 10: s = "\".\" expected"; break;
			case 11: s = "\"=\" expected"; break;
			case 12: s = "\",\" expected"; break;
			case 13: s = "\":\" expected"; break;
			case 14: s = "\"+\" expected"; break;
			case 15: s = "\"-\" expected"; break;
			case 16: s = "\"*\" expected"; break;
			case 17: s = "\"/\" expected"; break;
			case 18: s = "\"\\\\\" expected"; break;
			case 19: s = "\"&\" expected"; break;
			case 20: s = "\"^\" expected"; break;
			case 21: s = "\"?\" expected"; break;
			case 22: s = "\"{\" expected"; break;
			case 23: s = "\"}\" expected"; break;
			case 24: s = "\"(\" expected"; break;
			case 25: s = "\")\" expected"; break;
			case 26: s = "\">\" expected"; break;
			case 27: s = "\"<\" expected"; break;
			case 28: s = "\"<>\" expected"; break;
			case 29: s = "\">=\" expected"; break;
			case 30: s = "\"<=\" expected"; break;
			case 31: s = "\"<<\" expected"; break;
			case 32: s = "\">>\" expected"; break;
			case 33: s = "\"+=\" expected"; break;
			case 34: s = "\"^=\" expected"; break;
			case 35: s = "\"-=\" expected"; break;
			case 36: s = "\"*=\" expected"; break;
			case 37: s = "\"/=\" expected"; break;
			case 38: s = "\"\\\\=\" expected"; break;
			case 39: s = "\"<<=\" expected"; break;
			case 40: s = "\">>=\" expected"; break;
			case 41: s = "\"&=\" expected"; break;
			case 42: s = "\"AddHandler\" expected"; break;
			case 43: s = "\"AddressOf\" expected"; break;
			case 44: s = "\"Alias\" expected"; break;
			case 45: s = "\"And\" expected"; break;
			case 46: s = "\"AndAlso\" expected"; break;
			case 47: s = "\"Ansi\" expected"; break;
			case 48: s = "\"As\" expected"; break;
			case 49: s = "\"Assembly\" expected"; break;
			case 50: s = "\"Auto\" expected"; break;
			case 51: s = "\"Binary\" expected"; break;
			case 52: s = "\"Boolean\" expected"; break;
			case 53: s = "\"ByRef\" expected"; break;
			case 54: s = "\"Byte\" expected"; break;
			case 55: s = "\"ByVal\" expected"; break;
			case 56: s = "\"Call\" expected"; break;
			case 57: s = "\"Case\" expected"; break;
			case 58: s = "\"Catch\" expected"; break;
			case 59: s = "\"CBool\" expected"; break;
			case 60: s = "\"CByte\" expected"; break;
			case 61: s = "\"CChar\" expected"; break;
			case 62: s = "\"CDate\" expected"; break;
			case 63: s = "\"CDbl\" expected"; break;
			case 64: s = "\"CDec\" expected"; break;
			case 65: s = "\"Char\" expected"; break;
			case 66: s = "\"CInt\" expected"; break;
			case 67: s = "\"Class\" expected"; break;
			case 68: s = "\"CLng\" expected"; break;
			case 69: s = "\"CObj\" expected"; break;
			case 70: s = "\"Compare\" expected"; break;
			case 71: s = "\"Const\" expected"; break;
			case 72: s = "\"CShort\" expected"; break;
			case 73: s = "\"CSng\" expected"; break;
			case 74: s = "\"CStr\" expected"; break;
			case 75: s = "\"CType\" expected"; break;
			case 76: s = "\"Date\" expected"; break;
			case 77: s = "\"Decimal\" expected"; break;
			case 78: s = "\"Declare\" expected"; break;
			case 79: s = "\"Default\" expected"; break;
			case 80: s = "\"Delegate\" expected"; break;
			case 81: s = "\"Dim\" expected"; break;
			case 82: s = "\"DirectCast\" expected"; break;
			case 83: s = "\"Do\" expected"; break;
			case 84: s = "\"Double\" expected"; break;
			case 85: s = "\"Each\" expected"; break;
			case 86: s = "\"Else\" expected"; break;
			case 87: s = "\"ElseIf\" expected"; break;
			case 88: s = "\"End\" expected"; break;
			case 89: s = "\"EndIf\" expected"; break;
			case 90: s = "\"Enum\" expected"; break;
			case 91: s = "\"Erase\" expected"; break;
			case 92: s = "\"Error\" expected"; break;
			case 93: s = "\"Event\" expected"; break;
			case 94: s = "\"Exit\" expected"; break;
			case 95: s = "\"Explicit\" expected"; break;
			case 96: s = "\"False\" expected"; break;
			case 97: s = "\"Finally\" expected"; break;
			case 98: s = "\"For\" expected"; break;
			case 99: s = "\"Friend\" expected"; break;
			case 100: s = "\"Function\" expected"; break;
			case 101: s = "\"Get\" expected"; break;
			case 102: s = "\"GetType\" expected"; break;
			case 103: s = "\"GoSub\" expected"; break;
			case 104: s = "\"GoTo\" expected"; break;
			case 105: s = "\"Handles\" expected"; break;
			case 106: s = "\"If\" expected"; break;
			case 107: s = "\"Implements\" expected"; break;
			case 108: s = "\"Imports\" expected"; break;
			case 109: s = "\"In\" expected"; break;
			case 110: s = "\"Inherits\" expected"; break;
			case 111: s = "\"Integer\" expected"; break;
			case 112: s = "\"Interface\" expected"; break;
			case 113: s = "\"Is\" expected"; break;
			case 114: s = "\"Let\" expected"; break;
			case 115: s = "\"Lib\" expected"; break;
			case 116: s = "\"Like\" expected"; break;
			case 117: s = "\"Long\" expected"; break;
			case 118: s = "\"Loop\" expected"; break;
			case 119: s = "\"Me\" expected"; break;
			case 120: s = "\"Mod\" expected"; break;
			case 121: s = "\"Module\" expected"; break;
			case 122: s = "\"MustInherit\" expected"; break;
			case 123: s = "\"MustOverride\" expected"; break;
			case 124: s = "\"MyBase\" expected"; break;
			case 125: s = "\"MyClass\" expected"; break;
			case 126: s = "\"Namespace\" expected"; break;
			case 127: s = "\"New\" expected"; break;
			case 128: s = "\"Next\" expected"; break;
			case 129: s = "\"Not\" expected"; break;
			case 130: s = "\"Nothing\" expected"; break;
			case 131: s = "\"NotInheritable\" expected"; break;
			case 132: s = "\"NotOverridable\" expected"; break;
			case 133: s = "\"Object\" expected"; break;
			case 134: s = "\"Off\" expected"; break;
			case 135: s = "\"On\" expected"; break;
			case 136: s = "\"Option\" expected"; break;
			case 137: s = "\"Optional\" expected"; break;
			case 138: s = "\"Or\" expected"; break;
			case 139: s = "\"OrElse\" expected"; break;
			case 140: s = "\"Overloads\" expected"; break;
			case 141: s = "\"Overridable\" expected"; break;
			case 142: s = "\"Override\" expected"; break;
			case 143: s = "\"Overrides\" expected"; break;
			case 144: s = "\"ParamArray\" expected"; break;
			case 145: s = "\"Preserve\" expected"; break;
			case 146: s = "\"Private\" expected"; break;
			case 147: s = "\"Property\" expected"; break;
			case 148: s = "\"Protected\" expected"; break;
			case 149: s = "\"Public\" expected"; break;
			case 150: s = "\"RaiseEvent\" expected"; break;
			case 151: s = "\"ReadOnly\" expected"; break;
			case 152: s = "\"ReDim\" expected"; break;
			case 153: s = "\"RemoveHandler\" expected"; break;
			case 154: s = "\"Resume\" expected"; break;
			case 155: s = "\"Return\" expected"; break;
			case 156: s = "\"Select\" expected"; break;
			case 157: s = "\"Set\" expected"; break;
			case 158: s = "\"Shadows\" expected"; break;
			case 159: s = "\"Shared\" expected"; break;
			case 160: s = "\"Short\" expected"; break;
			case 161: s = "\"Single\" expected"; break;
			case 162: s = "\"Static\" expected"; break;
			case 163: s = "\"Step\" expected"; break;
			case 164: s = "\"Stop\" expected"; break;
			case 165: s = "\"Strict\" expected"; break;
			case 166: s = "\"String\" expected"; break;
			case 167: s = "\"Structure\" expected"; break;
			case 168: s = "\"Sub\" expected"; break;
			case 169: s = "\"SyncLock\" expected"; break;
			case 170: s = "\"Text\" expected"; break;
			case 171: s = "\"Then\" expected"; break;
			case 172: s = "\"Throw\" expected"; break;
			case 173: s = "\"To\" expected"; break;
			case 174: s = "\"True\" expected"; break;
			case 175: s = "\"Try\" expected"; break;
			case 176: s = "\"TypeOf\" expected"; break;
			case 177: s = "\"Unicode\" expected"; break;
			case 178: s = "\"Until\" expected"; break;
			case 179: s = "\"Variant\" expected"; break;
			case 180: s = "\"Wend\" expected"; break;
			case 181: s = "\"When\" expected"; break;
			case 182: s = "\"While\" expected"; break;
			case 183: s = "\"With\" expected"; break;
			case 184: s = "\"WithEvents\" expected"; break;
			case 185: s = "\"WriteOnly\" expected"; break;
			case 186: s = "\"Xor\" expected"; break;
			case 187: s = "\"Continue\" expected"; break;
			case 188: s = "\"Operator\" expected"; break;
			case 189: s = "\"Using\" expected"; break;
			case 190: s = "\"IsNot\" expected"; break;
			case 191: s = "\"SByte\" expected"; break;
			case 192: s = "\"UInteger\" expected"; break;
			case 193: s = "\"ULong\" expected"; break;
			case 194: s = "\"UShort\" expected"; break;
			case 195: s = "\"CSByte\" expected"; break;
			case 196: s = "\"CUShort\" expected"; break;
			case 197: s = "\"CUInt\" expected"; break;
			case 198: s = "\"CULng\" expected"; break;
			case 199: s = "\"Global\" expected"; break;
			case 200: s = "\"TryCast\" expected"; break;
			case 201: s = "\"Of\" expected"; break;
			case 202: s = "\"Narrowing\" expected"; break;
			case 203: s = "\"Widening\" expected"; break;
			case 204: s = "\"Partial\" expected"; break;
			case 205: s = "\"Custom\" expected"; break;
			case 206: s = "??? expected"; break;
			case 207: s = "invalid OptionStmt"; break;
			case 208: s = "invalid OptionStmt"; break;
			case 209: s = "invalid GlobalAttributeSection"; break;
			case 210: s = "invalid GlobalAttributeSection"; break;
			case 211: s = "invalid NamespaceMemberDecl"; break;
			case 212: s = "invalid OptionValue"; break;
			case 213: s = "invalid EndOfStmt"; break;
			case 214: s = "invalid TypeModifier"; break;
			case 215: s = "invalid NonModuleDeclaration"; break;
			case 216: s = "invalid NonModuleDeclaration"; break;
			case 217: s = "invalid Identifier"; break;
			case 218: s = "invalid TypeParameterConstraints"; break;
			case 219: s = "invalid PrimitiveTypeName"; break;
			case 220: s = "invalid MemberModifier"; break;
			case 221: s = "invalid StructureMemberDecl"; break;
			case 222: s = "invalid StructureMemberDecl"; break;
			case 223: s = "invalid StructureMemberDecl"; break;
			case 224: s = "invalid StructureMemberDecl"; break;
			case 225: s = "invalid StructureMemberDecl"; break;
			case 226: s = "invalid StructureMemberDecl"; break;
			case 227: s = "invalid StructureMemberDecl"; break;
			case 228: s = "invalid InterfaceMemberDecl"; break;
			case 229: s = "invalid InterfaceMemberDecl"; break;
			case 230: s = "invalid Charset"; break;
			case 231: s = "invalid VariableDeclarator"; break;
			case 232: s = "invalid AccessorDecls"; break;
			case 233: s = "invalid EventAccessorDeclaration"; break;
			case 234: s = "invalid OverloadableOperator"; break;
			case 235: s = "invalid VariableInitializer"; break;
			case 236: s = "invalid EventMemberSpecifier"; break;
			case 237: s = "invalid EventMemberSpecifier"; break;
			case 238: s = "invalid NonArrayTypeName"; break;
			case 239: s = "invalid SimpleExpr"; break;
			case 240: s = "invalid SimpleExpr"; break;
			case 241: s = "invalid SimpleExpr"; break;
			case 242: s = "invalid AssignmentOperator"; break;
			case 243: s = "invalid CastTarget"; break;
			case 244: s = "invalid InvocationOrMemberReferenceExpression"; break;
			case 245: s = "invalid InvocationExpression"; break;
			case 246: s = "invalid InvocationExpression"; break;
			case 247: s = "invalid RelationalExpr"; break;
			case 248: s = "invalid RelationalExpr"; break;
			case 249: s = "invalid Argument"; break;
			case 250: s = "invalid QualIdentAndTypeArguments"; break;
			case 251: s = "invalid AttributeArguments"; break;
			case 252: s = "invalid ParameterModifier"; break;
			case 253: s = "invalid Statement"; break;
			case 254: s = "invalid LabelName"; break;
			case 255: s = "invalid EmbeddedStatement"; break;
			case 256: s = "invalid EmbeddedStatement"; break;
			case 257: s = "invalid EmbeddedStatement"; break;
			case 258: s = "invalid EmbeddedStatement"; break;
			case 259: s = "invalid EmbeddedStatement"; break;
			case 260: s = "invalid EmbeddedStatement"; break;
			case 261: s = "invalid EmbeddedStatement"; break;
			case 262: s = "invalid WhileOrUntil"; break;
			case 263: s = "invalid OnErrorStatement"; break;
			case 264: s = "invalid ResumeStatement"; break;
			case 265: s = "invalid CaseClause"; break;
			case 266: s = "invalid CaseClause"; break;

			default: s = "error " + errorNumber; break;
		}
		errors.Error(line, col, s);
	}
	
	protected bool StartOf(int s)
	{
		return set[s, lexer.LookAhead.kind];
	}
	
	static bool[,] set = {
	{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,T,T,x, x,x,T,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,T,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,T,T,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,T,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,T,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x},
	{x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,T,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,x,T,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,T,x, x,x,x,x, T,T,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,T,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,x, x,x,x,T, x,x,x,x, x,x,x,x},
	{x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,T,T, x,x,x,x, x,x,T,T, T,T,x,x, x,x,x,x, x,x,T,x, x,T,x,x, x,x,x,T, T,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,T,T,T, x,x,x,x, x,x,x,T, T,x,x,x, x,x,x,x, T,T,x,T, x,x,T,T, T,T,x,T, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, T,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,T,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, T,x,x,x, x,x,x,x, T,T,x,T, x,x,T,x, T,T,x,T, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x},
	{x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,T, T,T,x,x, x,x,x,x, x,x,T,x, x,T,x,x, x,x,x,T, T,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,T,T,T, x,x,x,x, x,x,x,T, T,x,x,x, x,x,x,x, T,T,x,T, x,x,T,T, T,T,x,T, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x},
	{x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,T,T,T, T,T,T,T, T,T,T,x, x,T,T,T, T,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, T,x,T,x, T,x,x,T, T,T,T,T, T,T,T,x, T,T,T,T, T,T,T,T, T,T,x,x, x,T,T,T, T,x,x,x, T,x,x,T, T,x,T,x, T,x,T,x, x,x,T,x, T,x,T,x, x,x,x,T, x,x,x,x, x,T,x,T, x,x,x,x, T,T,x,T, x,x,T,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,T,T,T, T,x,x,x, T,T,T,x, T,x,T,x, x,T,T,x, T,x,T,T, T,x,x,x, x,x,T,T, x,x,x,T, x,T,x,T, T,T,T,T, T,T,T,T, T,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,T,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,T, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, T,x,x,x, x,x,x,x, T,T,x,T, x,x,T,T, T,T,x,T, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,T,T,T, T,T,T,T, T,T,T,x, x,T,T,T, T,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, T,x,T,x, T,x,x,T, T,T,T,T, T,T,T,x, T,T,T,T, T,T,T,T, T,T,x,x, x,T,T,T, T,x,x,x, x,x,x,T, T,x,T,x, T,x,T,x, x,x,T,x, T,x,T,x, x,x,x,T, x,x,x,x, x,T,x,T, x,x,x,x, T,T,x,T, x,x,T,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,T,T,T, T,x,x,x, T,T,T,x, T,x,T,x, x,T,T,x, T,x,T,T, T,x,x,x, x,x,T,T, x,x,x,T, x,T,x,T, T,T,T,T, T,T,T,T, T,x,x,x, x,x,x,x},
	{x,T,x,x, x,x,x,x, x,x,x,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,T,T, T,T,T,T, T,T,T,x, x,x,T,T, T,x,x,x, x,x,x,x, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,T, T,x,T,x, x,x,x,T, T,T,T,T, T,T,T,x, T,T,T,x, T,T,T,T, T,T,x,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,T,x, x,x,x,x, x,x,x,T, x,x,x,x, x,T,x,T, x,x,x,x, T,T,x,T, x,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,T,x, x,x,T,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,T, T,T,T,T, T,x,x,x, x,x,x,x},
	{x,x,T,T, T,T,T,T, T,T,T,x, x,x,T,T, T,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,T, T,x,T,x, x,x,x,T, T,T,T,T, T,T,T,x, T,T,T,x, T,T,T,T, T,T,x,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,T,x, x,x,x,x, x,x,x,T, x,x,x,x, x,T,x,T, x,x,x,x, T,T,x,T, x,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,T,x, x,x,T,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,T, T,T,T,T, T,x,x,x, x,x,x,x},
	{x,x,T,T, T,T,T,T, T,T,T,x, x,x,T,T, T,x,x,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,T, T,x,T,x, x,x,x,T, T,T,T,T, T,T,T,x, T,T,T,x, T,T,T,T, T,T,x,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,T,x, x,x,x,x, x,x,x,T, x,x,x,x, x,T,x,T, x,x,x,x, T,T,x,T, x,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,T,x, x,x,T,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,T, T,T,T,T, T,x,x,x, x,x,x,x},
	{x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,T,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,T,T, T,T,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,T, T,x,T,x, x,x,x,T, T,T,T,T, T,T,T,x, T,T,T,x, T,T,T,T, T,T,x,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,T,x, x,x,x,x, x,x,x,T, x,x,x,x, x,T,x,T, x,x,x,x, T,T,x,T, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,x,x, x,x,T,x, x,x,T,x, x,x,T,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, T,T,T,T, T,T,T,T, T,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,T,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,T,T, T,T,T,T, T,T,T,x, x,x,T,T, T,x,x,x, x,x,x,x, T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,T, T,x,T,x, T,x,x,T, T,T,T,T, T,T,T,x, T,T,T,x, T,T,T,T, T,T,x,x, x,x,T,T, T,x,x,x, x,x,x,T, T,x,T,x, T,x,T,x, x,x,T,x, T,x,T,x, x,x,x,T, x,x,x,x, x,T,x,T, x,x,x,x, T,T,x,T, x,x,T,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,T,T,T, T,x,x,x, T,T,x,x, T,x,T,x, x,T,T,x, T,x,T,T, T,x,x,x, x,x,T,T, x,x,x,T, x,T,x,T, T,T,T,T, T,T,T,T, T,x,x,x, x,x,x,x},
	{x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, T,T,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,T,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
	{x,x,x,x, x,x,x,x, x,x,x,T, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,T, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x}

	};
} // end Parser

}