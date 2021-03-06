﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using com.github.javaparser;
using com.github.javaparser.ast;
using com.github.javaparser.ast.body;
using JavaToCSharp.Declarations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace JavaToCSharp
{
    public static class JavaToCSharpConverter
    {
        public static string ConvertText(string javaText, JavaConversionOptions options = null)
        {
            if (options == null)
                options = new JavaConversionOptions();

            options.ConversionStateChanged(ConversionState.Starting);

            var context = new ConversionContext(options);

            var textBytes = Encoding.UTF8.GetBytes(javaText ?? string.Empty);

            using (var stringreader = new MemoryStream(textBytes))
            using (var wrapper = new ikvm.io.InputStreamWrapper(stringreader))
            {
                options.ConversionStateChanged(ConversionState.ParsingJavaAST);

                var parsed = JavaParser.parse(wrapper);

                options.ConversionStateChanged(ConversionState.BuildingCSharpAST);

                var types = parsed.getTypes().ToList<TypeDeclaration>();
                var imports = parsed.getImports().ToList<ImportDeclaration>();
                var package = parsed.getPackage();

                var usings = new List<UsingDirectiveSyntax>();

                //foreach (var import in imports)
                //{
                //    var usingSyntax = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(import.getName().toString()));
                //    usings.Add(usingSyntax);
                //}

                if (options.IncludeUsings)
                {
                    foreach (var ns in options.Usings.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        var usingSyntax = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns));
                        usings.Add(usingSyntax);
                    }
                }

                var rootMembers = new List<MemberDeclarationSyntax>();
                NamespaceDeclarationSyntax namespaceSyntax = null;

                if (options.IncludeNamespace)
                {
                    string packageName = package.getName().toString();

                    foreach (var packageReplacement in options.PackageReplacements)
                    {
                        packageName = packageReplacement.Replace(packageName);
                    }

                    packageName = TypeHelper.Capitalize(packageName);

                    namespaceSyntax = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(packageName));
                }

                foreach (var type in types)
                {
                    if (type is ClassOrInterfaceDeclaration)
                    {
                        var classOrIntType = type as ClassOrInterfaceDeclaration;

                        if (classOrIntType.isInterface())
                        {
                            var interfaceSyntax = ClassOrInterfaceDeclarationVisitor.VisitInterfaceDeclaration(context, classOrIntType, false);

                            if (options.IncludeNamespace)
                                namespaceSyntax = namespaceSyntax.AddMembers(interfaceSyntax);
                            else
                                rootMembers.Add(interfaceSyntax);
                        }
                        else
                        {
                            var classSyntax = ClassOrInterfaceDeclarationVisitor.VisitClassDeclaration(context, classOrIntType, false);

                            if (options.IncludeNamespace)
                                namespaceSyntax = namespaceSyntax.AddMembers(classSyntax);
                            else
                                rootMembers.Add(classSyntax);
                        }
                    }
                }

                if (options.IncludeNamespace)
                    rootMembers.Add(namespaceSyntax);

                var root = SyntaxFactory.CompilationUnit(
                    externs: new SyntaxList<ExternAliasDirectiveSyntax>(),
                    usings: SyntaxFactory.List(usings.ToArray()),
                    attributeLists: new SyntaxList<AttributeListSyntax>(),
                    members: SyntaxFactory.List<MemberDeclarationSyntax>(rootMembers))
                    .NormalizeWhitespace();

                var tree = SyntaxFactory.SyntaxTree(root);

                options.ConversionStateChanged(ConversionState.Done);

                return tree.GetText().ToString();
            }
        }
    }
}
