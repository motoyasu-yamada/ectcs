# ectcs
Fastest C# template engine with ect syntax

## Feature
This template engine compiles templates to native C# runtime by using Linq.Expression

## Usage
See ectcs/ectcs_test/SampleTest.cs
```cs
var ect = new Ect();
var html = ect.Render("page",  new { title = "Hello, World!" });
```

## Deference from original JavaScript version
- Use paren '(', ')' when calling function
- Only a part of CoffeScript expressions are supported

## Known bug
- Ident feature is not supported now

## License
(The MIT License)
Copyright (c) 2016 Motoyasu Yamada

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the 'Software'), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


## Original ect (Fastest JavaScript template engine) version
Copyright (c) 2012 Vadim M. Baryshev <vadimbaryshev@gmail.com>
http://ectjs.com/
https://github.com/baryshev/ect
